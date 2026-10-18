using System.Diagnostics;
using System.Diagnostics.Metrics;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Configuration.Metrics;

/// <summary>
/// Business instruments of the live pipeline: candles in, rule evaluations and alert
/// deliveries. Every instrument is a static long-lived field (the metrics API
/// warns that creating instruments per call is the expensive mistake); the helpers are thin
/// wrappers so call sites stay one line and never throw. Prometheus names are the instrument
/// name plus the unit suffix the exporter appends (counters get <c>_total</c>).
/// </summary>
public static class BusinessMetrics
{
  private const string ProviderTag = "provider";
  private const string ChannelTag = "channel";
  private const string IndicatorTag = "indicator";
  private const string StatusTag = "status";

  // --- Candles / providers ---

  private static readonly Counter<long> CandlesReceived =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_candles_received", description: "Candles ingested from a provider into the buffer");

  private static readonly Gauge<double> CandleLag =
    StockIndicatorsMetrics.Meter.CreateGauge<double>("si_candle_lag_seconds", description: "Age of the newest arrived candle (wall clock minus candle timestamp), by provider");

  private static readonly Histogram<double> PollDuration =
    StockIndicatorsMetrics.Meter.CreateHistogram<double>("si_provider_poll_duration", "s", "One provider polling cycle duration");

  private static readonly Counter<long> ProviderPollFailures =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_provider_poll_failures", description: "Provider polling cycles that ended in an error");

  private static readonly Gauge<double> TrackedInstruments =
    StockIndicatorsMetrics.Meter.CreateGauge<double>("si_provider_tracked_instruments", description: "Instruments a provider consumer is currently subscribed to");

  private static readonly Counter<long> StreamReconnects =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_provider_stream_reconnects", description: "Streaming (Tinkoff) reconnects");

  private static readonly Counter<long> CandlesFinalized =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_candles_finalized", description: "Reduced candles that reached the finalizer (cache + subscription check)");

  private static readonly Histogram<double> FinalizerDuration =
    StockIndicatorsMetrics.Meter.CreateHistogram<double>("si_candle_finalizer_duration", "s", "Duration of one candle finalizer cycle (batch reduce + publish)");

  // --- Rule engine ---

  private static readonly Counter<long> SubscriptionsChecked =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_subscriptions_checked", description: "Subscription checks run over candle batches");

  private static readonly Histogram<double> CheckBatchDuration =
    StockIndicatorsMetrics.Meter.CreateHistogram<double>("si_check_batch_duration", "s", "Duration of one subscription check batch (evaluations + flush + commit)");

  private static readonly Counter<long> ConditionEvaluations =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_condition_evaluations", description: "Leaf condition evaluations by indicator and outcome");

  private static readonly Histogram<double> ConditionEvaluationDuration =
    StockIndicatorsMetrics.Meter.CreateHistogram<double>("si_condition_evaluation_duration", "s", "Leaf indicator evaluation duration by indicator");

  private static readonly Counter<long> RulesTriggered =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_rules_triggered", description: "Rules whose condition was met and ran their actions");

  // --- Alerts / deliveries ---

  private static readonly Gauge<double> AlertQueueDepth =
    StockIndicatorsMetrics.Meter.CreateGauge<double>("si_alert_queue_depth", description: "Alerts waiting in the dispatcher for the next flush");

  private static readonly Counter<long> AlertsEnqueued =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_alerts_enqueued", description: "Alerts queued for delivery by a triggered rule");

  private static readonly Counter<long> AlertDeliveries =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_alert_deliveries", description: "Per-channel delivery outcomes (sent/failed/skipped)");

  private static readonly Histogram<double> AlertSendDuration =
    StockIndicatorsMetrics.Meter.CreateHistogram<double>("si_alert_send_duration", "s", "Per-channel sender duration");

  private static readonly Histogram<double> AlertFlushSize =
    StockIndicatorsMetrics.Meter.CreateHistogram<double>("si_alert_flush_size", description: "Alerts flushed in one dispatcher run");

  private static readonly Counter<long> AccountDeletions =
    StockIndicatorsMetrics.Meter.CreateCounter<long>("si_account_deletions", description: "Accounts deleted by the deletion sweep");

  // --- Candles / providers helpers ---

  public static void CandleReceived(string provider, DateTime newestCandleTimestamp, int count = 1)
  {
    var tags = new TagList { { ProviderTag, provider } };
    CandlesReceived.Add(count, tags);
    CandleLag.Record(Math.Max(0, (DateTime.UtcNow - newestCandleTimestamp).TotalSeconds), tags);
  }

  public static void ProviderPollDuration(string provider, double seconds) =>
    PollDuration.Record(seconds, new TagList { { ProviderTag, provider } });

  public static void ProviderPollFailure(string provider) => ProviderPollFailures.Add(1, new TagList { { ProviderTag, provider } });

  public static void TrackedInstrumentsCount(string provider, int count) => TrackedInstruments.Record(count, new TagList { { ProviderTag, provider } });

  public static void StreamReconnect(string provider) => StreamReconnects.Add(1, new TagList { { ProviderTag, provider } });

  /// <summary>
  /// Registers the provider's push-silence gauge: wall-clock seconds since that provider's
  /// last candle push, zeroed by the provider itself whenever no candles are expected (its
  /// trading sessions), so a feed that answers 200-OK empty pages stalls the pipeline
  /// visibly even though the poll loop keeps completing cycles. Observable on purpose: a
  /// synchronous gauge could not update while the polling loop is itself blocked, a scrape
  /// callback can. Called once per provider consumer at composition time; the callback runs
  /// on the scrape thread and must never throw.
  /// </summary>
  public static void RegisterCandlePushSilence(string provider, Func<double> silenceSeconds)
  {
    StockIndicatorsMetrics.Meter.CreateObservableGauge(
      "si_candle_push_silence",
      () => new Measurement<double>(silenceSeconds(), new TagList { { ProviderTag, provider } }),
      unit: "s",
      description: "Seconds since the provider's last candle push (zero while its market session is closed)");
  }

  public static void CandlesFinalizedCount(int count) => CandlesFinalized.Add(count);

  public static void FinalizerDurationSeconds(double seconds) => FinalizerDuration.Record(seconds);

  // --- Rule engine helpers ---

  public static void SubscriptionsCheckedCount(int count) => SubscriptionsChecked.Add(count);

  public static void CheckBatchDurationSeconds(double seconds) => CheckBatchDuration.Record(seconds);

  public static void ConditionEvaluated(Indicator indicator, bool met, double seconds)
  {
    var tags = new TagList
    {
      { IndicatorTag, StockIndicatorsMetrics.Label(indicator) },
      { StatusTag, met ? "met" : "unmet" }
    };
    ConditionEvaluations.Add(1, tags);
    ConditionEvaluationDuration.Record(seconds, new TagList { { IndicatorTag, StockIndicatorsMetrics.Label(indicator) } });
  }

  public static void RuleTriggered() => RulesTriggered.Add(1);

  // --- Alerts / deliveries helpers ---

  public static void AlertEnqueued() => AlertsEnqueued.Add(1);

  public static void AlertQueueDepthSet(int depth) => AlertQueueDepth.Record(depth);

  public static void AlertFlushSizeRecord(int size) => AlertFlushSize.Record(size);

  public static void AlertDelivery(NotificationChannel channel, DeliveryStatus status) =>
    AlertDeliveries.Add(1, new TagList { { ChannelTag, StockIndicatorsMetrics.Label(channel) }, { StatusTag, StockIndicatorsMetrics.Label(status) } });

  public static void AlertSendDurationRecord(NotificationChannel channel, double seconds) =>
    AlertSendDuration.Record(seconds, new TagList { { ChannelTag, StockIndicatorsMetrics.Label(channel) } });

  public static void AccountDeleted() => AccountDeletions.Add(1);
}
