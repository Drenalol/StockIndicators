using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Configuration.Metrics;

/// <summary>
/// An immutable snapshot of the cheap database counts behind the <c>si_*</c> observable
/// gauges. The scrape callback only reads the last snapshot; a background service refreshes
/// it on a timer, so a scrape never waits on a database round trip.
/// </summary>
public sealed record DatabaseGaugeSnapshot(
  long Users,
  IReadOnlyDictionary<SubscriptionStatus, long> SubscriptionsByStatus,
  IReadOnlyDictionary<PushPlatform, long> PushSubscriptionsByPlatform)
{
  public static DatabaseGaugeSnapshot Empty { get; } =
    new(0, new Dictionary<SubscriptionStatus, long>(), new Dictionary<PushPlatform, long>());
}
