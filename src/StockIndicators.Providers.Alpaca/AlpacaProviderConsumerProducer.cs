using System.Reactive;
using System.Reactive.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using Alpaca.Markets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Application.Rules;
using StockIndicators.Providers.Alpaca.Api;
using StockIndicators.Providers.Base.Exchange;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Providers.Alpaca.Mappers;
using StockIndicators.Providers.Base;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Alpaca;

/// <summary>
/// Polls minute bars for tracked US symbols and feeds the shared candle pipeline.
/// A subscription is tracked only when its exchange belongs to the Alpaca (US) universe,
/// so MOEX subscriptions are never activated by US candles and vice versa.
/// </summary>
public class AlpacaProviderConsumerProducer : ConsumerProviderBase, ICandleSource, IDisposable
{
  // Case-insensitive: the hub's chart-view claims and the subscription path pass the
  // exchange in different casings, and a miss here would silently skip tracking
  private static readonly HashSet<string> UsExchanges = new(StringComparer.OrdinalIgnoreCase)
  {
    "NASDAQ", "NYSE", "ARCA", "BATS", "OTC", "AMEX", "IEX", "NYSE MKT"
  };

  private readonly AlpacaProvider _provider;
  private readonly AlpacaProviderClient _client;
  private readonly AlpacaProviderOptions _options;
  private readonly ICandleProvider _candleBuffer;
  private readonly IInstrumentProvider _instrumentProvider;
  private readonly IAlpacaBoatsApi _boatsApi;
  private readonly ExchangeSession _session;
  private readonly CancellationTokenSource _cts = new();
  private readonly SemaphoreSlim _lock = new(1, 1);
  // Keyed by symbol as returned by the API; removals arrive with the hub/subscription
  // casing, so the dictionary must match symbols case-insensitively
  private readonly Dictionary<string, string> _exchanges = new(StringComparer.OrdinalIgnoreCase);
  // Symbols whose tracking lookup failed transiently (throttle, timeout, provider hiccup).
  // Without a retry a single failed lookup left the instrument untracked for the whole
  // lifetime of its watch claim: the five retry rounds only run at startup, while claims
  // arrive whenever a chart page opens. The polling loop gives these another try.
  private readonly HashSet<string> _retryTracking = new(StringComparer.OrdinalIgnoreCase);
  private const int MaxRetryLookupsPerCycle = 25;
  // Free tier locks the latest 15 minutes of SIP data, so the window ends 16 minutes in
  // the past. It starts at the previous cycle's end (plus a 3-minute overlap that also
  // re-covers a slow cycle) instead of a fixed offset: with the fixed 3-minute window a
  // failed or stalled cycle left its minutes permanently out of the cache - the next
  // window had already moved past them - while a self-extended one re-covers everything
  // the stall missed on the first success. The heal span is capped: much older holes
  // belong to the request-path history backfill, not to the live pipeline.
  internal static readonly TimeSpan PollWindowEndOffset = TimeSpan.FromMinutes(16);
  internal static readonly TimeSpan PollWindowOverlap = TimeSpan.FromMinutes(3);
  internal static readonly TimeSpan PollWindowMaxHeal = TimeSpan.FromHours(2);
  // End of the window the last fully successful cycle consumed (polling loop only)
  private DateTime _lastPolledEndUtc;
  // Wall clock of the last non-empty poll, as UTC ticks (volatile-read by the scrape
  // callback of the push-silence gauge, which runs outside the polling loop)
  private long _lastPushTicks;
  // Mirrors _exchanges.Count for the silence gauge's scrape callback, which must not
  // take the polling lock
  private int _trackedCount;
  private readonly IEnumerable<IDisposable> _subscriptions;

  public AlpacaProviderConsumerProducer(
    AlpacaProvider provider,
    AlpacaProviderClient client,
    IOptions<AlpacaProviderOptions> options,
    ICandleProvider candleBuffer,
    IInstrumentProvider instrumentProvider,
    IAlpacaBoatsApi boatsApi,
    [FromKeyedServices(ExchangeSessions.Us)] ExchangeSession session,
    ILogger logger
  ) : base(provider.ProviderCode, logger)
  {
    _provider = provider;
    _client = client;
    _options = options.Value;
    _candleBuffer = candleBuffer;
    _instrumentProvider = instrumentProvider;
    _boatsApi = boatsApi;
    _session = session;

    // A process starting into an already-stalled feed must count its silence from its own
    // start, not wait for a first push that may never come
    Volatile.Write(ref _lastPushTicks, session.UtcNow.UtcDateTime.Ticks);
    BusinessMetrics.RegisterCandlePushSilence(ProviderMetricLabel, ComputePushSilenceSeconds);

    _subscriptions =
    [
      instrumentProvider.OnAdded
        .SelectMany(TrackAsync)
        .Subscribe(),
      instrumentProvider.OnRemoved
        .SelectMany(UntrackAsync)
        .Subscribe()
    ];
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    // Pick up instruments added before this consumer started (subscription restore).
    // Exchange-aware: only US-bound (or legacy null-exchange) subscriptions are tracked.
    _ = RestoreTrackingAsync(_cts.Token);

    _ = PollingLoopAsync(_cts.Token);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Restores tracking for the persisted instruments sequentially, so the instrument
  /// limiter paces the lookups. A concurrent burst would leave every request beyond the
  /// limiter's first token batch idling past the per-lookup timeout in
  /// <see cref="AlpacaProviderClient.GetInstrumentAsync"/> and silently untracked.
  /// Lookups throttled by Alpaca (429 under the startup history-backfill contention,
  /// which shares the same server-side budget) are retried in later rounds.
  /// </summary>
  private async Task RestoreTrackingAsync(CancellationToken cancellationToken)
  {
    var pending = _instrumentProvider.Keys.ToList();

    for (var round = 0; round < 5 && pending.Count > 0 && !cancellationToken.IsCancellationRequested; round++)
    {
      var failed = new List<InstrumentKey>();

      foreach (var key in pending)
        if (!await TryTrackAsync(key, cancellationToken))
          failed.Add(key);

      pending = failed;

      if (pending.Count > 0)
      {
        try
        {
          await Task.Delay(TimeSpan.FromSeconds(70), cancellationToken);
        }
        catch (OperationCanceledException)
        {
          return;
        }
      }
    }

    if (pending.Count > 0)
      Logger.Warning("Alpaca restore tracking left {Count} instruments untracked", pending.Count);
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _cts.Cancel();
    return Task.CompletedTask;
  }

  public void Dispose()
  {
    _cts.Dispose();
    _lock.Dispose();
    foreach (var subscription in _subscriptions)
      subscription.Dispose();
  }

  private async Task<Unit> TrackAsync(InstrumentKey key, CancellationToken cancellationToken)
  {
    await TryTrackAsync(key, cancellationToken);
    return Unit.Default;
  }

  /// <summary>
  /// True when the instrument is tracked or is not a US one (nothing to do);
  /// false only when the lookup failed and tracking must be retried later.
  /// </summary>
  private async Task<bool> TryTrackAsync(InstrumentKey key, CancellationToken cancellationToken)
  {
    if (key.ExchangeName != null && !UsExchanges.Contains(key.ExchangeName))
      return true;

    try
    {
      var instrument = await _client.GetInstrumentAsync(key.Ticker, cancellationToken);

      if (instrument.ExchangeName == null || !UsExchanges.Contains(instrument.ExchangeName))
        return true;

      await _lock.WaitAsync(cancellationToken);
      try
      {
        _exchanges[instrument.Ticker] = instrument.ExchangeName!;
        Volatile.Write(ref _trackedCount, _exchanges.Count);
        _retryTracking.Remove(key.Ticker);
        Logger.Information("Alpaca tracking {Ticker} ({Exchange})", instrument.Ticker, instrument.ExchangeName);
      }
      finally
      {
        _lock.Release();
      }

      return true;
    }
    catch (Exception e) when (e is RestClientErrorException { HttpStatusCode: HttpStatusCode.NotFound })
    {
      // A real Alpaca 404: the symbol simply has no asset in the universe, nothing to poll.
      // This arm used to be the catch-all - every transient lookup failure (and every
      // lowercase symbol sent to the asset endpoint) hid behind the same Debug line,
      // invisible in production, and the instrument stayed untracked forever.
      Logger.Debug("Alpaca: {Ticker} not in the asset universe ({Reason})", key.Ticker, e.Message);
      return true;
    }
    catch (OperationCanceledException)
    {
      // Aborted request or shutdown: the caller's own rounds own the retry
      return false;
    }
    catch (Exception e)
    {
      await _lock.WaitAsync(cancellationToken);
      try
      {
        if (_retryTracking.Add(key.Ticker))
          Logger.Warning(e, "Alpaca tracking lookup of {Ticker} failed, retrying in the polling loop", key.Ticker);
      }
      finally
      {
        _lock.Release();
      }

      return false;
    }
  }

  private async Task<Unit> UntrackAsync(InstrumentKey key, CancellationToken cancellationToken)
  {
    if (key.ExchangeName != null && !UsExchanges.Contains(key.ExchangeName))
      return Unit.Default;

    await _lock.WaitAsync(cancellationToken);
    try
    {
      if (_exchanges.Remove(key.Ticker))
      {
        Volatile.Write(ref _trackedCount, _exchanges.Count);
        Logger.Information("Alpaca untracked {Ticker}", key.Ticker);
      }
    }
    finally
    {
      _lock.Release();
    }

    return Unit.Default;
  }

  private async Task PollingLoopAsync(CancellationToken cancellationToken)
  {
    var interval = TimeSpan.FromSeconds(_options.CandlePollIntervalSeconds);

    while (!cancellationToken.IsCancellationRequested)
    {
      var stopwatch = Stopwatch.StartNew();

      try
      {
        await RetryTrackingAsync(cancellationToken);
        await PollCandlesAsync(cancellationToken);
        BusinessMetrics.ProviderPollDuration(ProviderMetricLabel, stopwatch.Elapsed.TotalSeconds);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception e)
      {
        BusinessMetrics.ProviderPollDuration(ProviderMetricLabel, stopwatch.Elapsed.TotalSeconds);
        BusinessMetrics.ProviderPollFailure(ProviderMetricLabel);
        Logger.Warning(e, "Alpaca polling cycle failed");
      }

      try
      {
        await Task.Delay(interval, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }
    }
  }

  /// <summary>
  /// Gives failed tracking lookups another paced try on every polling cycle: a claim
  /// arrives whenever a chart page opens, and one transient lookup failure used to leave
  /// the instrument untracked until the page reconnected. Symbols that left the watch
  /// list in the meantime are dropped instead of resurrected. The lookups go through the
  /// instruments limiter (no session gate), so retries also progress outside trading hours.
  /// </summary>
  private async Task RetryTrackingAsync(CancellationToken cancellationToken)
  {
    string[] pending;

    await _lock.WaitAsync(cancellationToken);
    try
    {
      if (_retryTracking.Count == 0)
        return;

      var stale = _retryTracking.Where(ticker => !_instrumentProvider.Instruments.Contains(ticker)).ToArray();
      foreach (var ticker in stale)
        _retryTracking.Remove(ticker);

      pending = _retryTracking.Take(MaxRetryLookupsPerCycle).ToArray();
    }
    finally
    {
      _lock.Release();
    }

    foreach (var ticker in pending)
      if (!await TryTrackAsync(new InstrumentKey(ticker, null), cancellationToken))
        Logger.Debug("Alpaca retry tracking of {Ticker} failed again", ticker);
  }

  /// <summary>
  /// Minute-poll window: ends at the free-tier delay horizon, starts where the last fully
  /// successful cycle stopped (see <see cref="PollWindowEndOffset"/>). A first cycle and a
  /// normal one both get the short overlapping span; only a stalled pipeline extends it.
  /// </summary>
  internal static (DateTime Start, DateTime End) ComputePollWindow(DateTime nowUtc, DateTime lastPolledEndUtc)
  {
    var end = nowUtc - PollWindowEndOffset;
    var healFrom = lastPolledEndUtc == default ? end : lastPolledEndUtc;

    if (healFrom < end - PollWindowMaxHeal)
      healFrom = end - PollWindowMaxHeal;

    return (healFrom - PollWindowOverlap, end);
  }

  private async Task PollCandlesAsync(CancellationToken cancellationToken)
  {
    await _lock.WaitAsync(cancellationToken);
    var tickers = _exchanges.Count > 0 ? _exchanges.ToList() : [];
    Volatile.Write(ref _trackedCount, _exchanges.Count);
    _lock.Release();

    BusinessMetrics.TrackedInstrumentsCount(ProviderMetricLabel, tickers.Count);

    if (tickers.Count == 0)
      return;

    var (windowStart, windowEnd) = ComputePollWindow(DateTime.UtcNow, _lastPolledEndUtc);

    var request = new HistoricalBarsRequest(tickers.Select(ticker => ticker.Key), windowStart, windowEnd, BarTimeFrame.Minute)
    {
      Adjustment = Adjustment.SplitsOnly,
      Feed = MarketDataFeed.Sip
    }.WithPageSize(Pagination.MaxPageSize);

    await _provider.PollingLimiter.WaitAsync(cancellationToken);
    var page = await _provider.DataClient.GetHistoricalBarsAsync(request, cancellationToken);
    var pushed = 0;
    DateTime newest = default;

    while (true)
    {
      // Every bar in the delayed window is final: the mapper already marks
      // candles as completed.
      foreach (var (symbol, bars) in page.Items)
      {
        foreach (var bar in bars)
        {
          var candle = bar.ToCandle(TimeFrame.Minute, _exchanges.GetValueOrDefault(symbol));
          await _candleBuffer.PushAsync(candle, cancellationToken);
          pushed++;

          if (candle.Timestamp > newest)
            newest = candle.Timestamp;
        }
      }

      if (page.NextPageToken is null)
        break;

      await _provider.PollingLimiter.WaitAsync(cancellationToken);
      request = request.WithPageToken(page.NextPageToken);
      page = await _provider.DataClient.GetHistoricalBarsAsync(request, cancellationToken);
    }

    if (pushed > 0)
      MarkCandlesPushed(pushed, newest);

    Logger.Debug("Alpaca polled {Tickers} tickers, pushed {Pushed} minute candles", tickers.Count, pushed);

    // The BOATS feed is the only source of the overnight session (20:00-04:00, delayed
    // 15 minutes like the rest of the free tier): the SIP window comes back empty during
    // the session, so the overnight minutes are polled right after it. A window touching
    // the session is fetched from both feeds to cover the boundary minutes cleanly.
    var windowStartOffset = new DateTimeOffset(windowStart, TimeSpan.Zero);
    var windowEndOffset = new DateTimeOffset(windowEnd, TimeSpan.Zero);
    var boatsPushed = 0;

    if (_session.ExtendedKindAt(windowStartOffset) == ExtendedSessionKind.Overnight ||
        _session.ExtendedKindAt(windowEndOffset) == ExtendedSessionKind.Overnight)
      boatsPushed = await PollBoatsCandlesAsync(string.Join(",", tickers.Select(ticker => ticker.Key)), windowStart, windowEnd, cancellationToken);

    // Only a fully successful cycle (SIP pages and the overnight feed it touched) consumes
    // the window: a failure anywhere above leaves the marker behind, so the next cycle's
    // window grows over the missed minutes instead of losing them forever. An all-empty
    // cycle over an open session is treated the same way - a 200-OK answer with no bars
    // mid-session is an upstream stall (the Alpaca vendor outage of 2026-10-15 served
    // empty pages for an hour at 200), and consuming the window through it would turn
    // the stall into a hole the self-healing span never re-covers.
    if (ShouldConsumePollWindow(_session, windowStart, windowEnd, pushed + boatsPushed))
      _lastPolledEndUtc = windowEnd;
  }

  /// <summary>
  /// An empty window that touches an open (regular or extended) session must not consume
  /// the polling marker: bars for those minutes exist and will reappear when the upstream
  /// feed heals, so the self-healing window has to stay anchored behind the stall. Outside
  /// sessions an empty window is simply the market being closed and is consumed normally.
  /// </summary>
  internal static bool ShouldConsumePollWindow(ExchangeSession session, DateTime windowStartUtc, DateTime windowEndUtc, int pushed)
  {
    if (pushed > 0)
      return true;

    return !SessionOpenAt(session, windowStartUtc) && !SessionOpenAt(session, windowEndUtc);
  }

  // The polling gate's notion of "bars should be flowing": regular or any extended session
  private static bool SessionOpenAt(ExchangeSession session, DateTime utc)
  {
    var at = new DateTimeOffset(utc, TimeSpan.Zero);
    return session.IsOpenAt(at) || session.ExtendedKindAt(at) is not null;
  }

  private void MarkCandlesPushed(int pushed, DateTime newestCandle)
  {
    BusinessMetrics.CandleReceived(ProviderMetricLabel, newestCandle, pushed);
    Volatile.Write(ref _lastPushTicks, _session.UtcNow.UtcDateTime.Ticks);
  }

  /// <summary>
  /// Wall-clock seconds since the last candle push, read by the push-silence gauge's scrape
  /// callback (so it keeps growing even while the polling loop itself is blocked). Zero
  /// while the delayed feed horizon sits outside an open session or nothing is tracked:
  /// no candles are expected there, so the stall alert needs no trading calendar of its own.
  /// </summary>
  internal double ComputePushSilenceSeconds()
  {
    if (Volatile.Read(ref _trackedCount) == 0)
      return 0;

    var now = _session.UtcNow;
    var horizonUtc = (now - PollWindowEndOffset).UtcDateTime;

    if (!SessionOpenAt(_session, horizonUtc))
      return 0;

    var lastPush = new DateTime(Volatile.Read(ref _lastPushTicks), DateTimeKind.Utc);
    return Math.Max(0, (now - lastPush).TotalSeconds);
  }

  private async Task<int> PollBoatsCandlesAsync(
    string symbols,
    DateTime windowStart,
    DateTime windowEnd,
    CancellationToken cancellationToken)
  {
    var start = windowStart.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
    var end = windowEnd.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    string? pageToken = null;
    var pushed = 0;
    DateTime newest = default;

    while (true)
    {
      await _provider.PollingLimiter.WaitAsync(cancellationToken);

      var page = await _boatsApi.GetBarsAsync(
        symbols, "1Min", start, end, (int) Pagination.MaxPageSize, "boats", pageToken, _options.ApiKey!, _options.ApiSecret!, cancellationToken);

      if (page.Bars is { } barsBySymbol)
      {
        foreach (var (symbol, bars) in barsBySymbol)
        {
          foreach (var bar in bars)
          {
            // Every delayed-window bar is final, the same convention as the SIP polling
            var candle = new Candle(symbol, DateTime.SpecifyKind(bar.TimeUtc, DateTimeKind.Utc), TimeFrame.Minute, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, bar.TradeCount > 0, true, _exchanges.GetValueOrDefault(symbol));
            await _candleBuffer.PushAsync(candle, cancellationToken);

            pushed++;

            if (candle.Timestamp > newest)
              newest = candle.Timestamp;
          }
        }
      }

      if (page.NextPageToken is null)
        break;

      pageToken = page.NextPageToken;
    }

    if (pushed > 0)
      MarkCandlesPushed(pushed, newest);

    Logger.Debug("Alpaca polled boats symbols, pushed {Pushed} overnight minute candles", pushed);

    return pushed;
  }
}
