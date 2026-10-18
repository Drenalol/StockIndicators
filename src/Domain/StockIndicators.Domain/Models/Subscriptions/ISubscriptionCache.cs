namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// Alert-cooldown store: a subscription that has just fired is not re-checked until its
/// check interval passes. Keyed by subscription id only (the subscription itself no longer
/// carries an instrument).
/// </summary>
public interface ISubscriptionCache
{
  Task InvalidateAsync(Subscription subscription, CancellationToken cancellationToken = default);
  Task SetTriggeredAsync(Subscription subscription, CancellationToken cancellationToken = default);
  Task<bool> IsTriggeredAsync(Subscription subscription, CancellationToken cancellationToken = default);
}
