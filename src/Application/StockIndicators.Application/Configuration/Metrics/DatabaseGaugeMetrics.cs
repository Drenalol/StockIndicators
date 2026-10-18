using System.Diagnostics.Metrics;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Configuration.Metrics;

/// <summary>
/// Observable gauges over the <see cref="DatabaseGaugeSnapshot"/>: users, subscriptions by
/// status and push subscriptions by platform. The
/// callbacks are allocation-light reads of the last snapshot; the refresh lives in the
/// Infrastructure gauge service.
/// </summary>
public static class DatabaseGaugeMetrics
{
  private static volatile DatabaseGaugeSnapshot _snapshot = DatabaseGaugeSnapshot.Empty;

  public static void SetSnapshot(DatabaseGaugeSnapshot snapshot) => _snapshot = snapshot;

  private static readonly ObservableGauge<long> Users =
    StockIndicatorsMetrics.Meter.CreateObservableGauge<long>("si_users_total", ObserveUsers, description: "Registered accounts");

  private static readonly ObservableGauge<long> Subscriptions =
    StockIndicatorsMetrics.Meter.CreateObservableGauge<long>("si_subscriptions_total", ObserveSubscriptions, description: "Subscriptions by status");

  private static readonly ObservableGauge<long> PushSubscriptions =
    StockIndicatorsMetrics.Meter.CreateObservableGauge<long>("si_push_subscriptions_total", ObservePushSubscriptions, description: "Push subscriptions by platform");

  private static long ObserveUsers() => _snapshot.Users;

  private static IEnumerable<Measurement<long>> ObserveSubscriptions() =>
    _snapshot.SubscriptionsByStatus.Select(pair =>
      new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("status", StockIndicatorsMetrics.Label(pair.Key))));

  private static IEnumerable<Measurement<long>> ObservePushSubscriptions() =>
    _snapshot.PushSubscriptionsByPlatform.Select(pair =>
      new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("platform", StockIndicatorsMetrics.Label(pair.Key))));
}
