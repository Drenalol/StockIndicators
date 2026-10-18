using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;

/// <summary>
/// Nothing was rendered for the subscription (the cache holds no candles for any of its
/// instruments) - the snapshot has no data to serve. The client-facing text stays neutral
/// (the cache internals are not the user's problem), maps to 404.
/// </summary>
public class SnapshotNotFoundException : NotFoundException
{
  public SnapshotNotFoundException() : base("The current chart is not available right now, please try again later")
  {
  }
}
