using LazyCache;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

/// <summary>
/// Alert-cooldown store keyed by subscription id. A fired subscription is skipped by the
/// check pipeline until its cooldown (Subscription.Cooldown) has passed; the indicator
/// state cache slot is dropped together with it.
/// </summary>
public class SubscriptionInMemoryCache : ISubscriptionCache
{
  private readonly IAppCache _appCache;
  private readonly IIndicatorCache _indicatorCache;

  public SubscriptionInMemoryCache(IAppCache appCache, IIndicatorCache indicatorCache)
  {
    _appCache = appCache;
    _indicatorCache = indicatorCache;
  }

  public async Task InvalidateAsync(Subscription subscription, CancellationToken cancellationToken = default)
  {
    _appCache.Remove(CreateKey(subscription.Id));
    await _indicatorCache.InvalidateAsync(subscription.Id, cancellationToken);
  }

  public Task<bool> IsTriggeredAsync(Subscription subscription, CancellationToken cancellationToken = default)
  {
    var key = CreateKey(subscription.Id);

    // The value is stored as DateTimeOffset by SetTriggeredAsync — LazyCache 2.x casts hard on read, so types must match
    return Task.FromResult(_appCache.TryGetValue<DateTimeOffset>(key, out _));
  }

  public Task SetTriggeredAsync(Subscription subscription, CancellationToken cancellationToken = default)
  {
    var expires = DateTimeOffset.UtcNow.Add(subscription.Cooldown);
    _appCache.Add(CreateKey(subscription.Id), expires, expires);

    return Task.CompletedTask;
  }

  private static string CreateKey(Guid subscriptionId) => $"subscriptions:{subscriptionId}:triggered";
}
