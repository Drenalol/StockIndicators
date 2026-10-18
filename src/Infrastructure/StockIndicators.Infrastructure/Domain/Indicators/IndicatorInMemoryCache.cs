using LazyCache;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Infrastructure.Domain.Indicators;

public class IndicatorInMemoryCache : IIndicatorCache
{
  private readonly IAppCache _cache;

  public IndicatorInMemoryCache(IAppCache cache) => _cache = cache;

  public Task<T> GetOrAddAsync<T>(Guid subscriptionId, Func<T> factory, CancellationToken cancellationToken = default) where T : IndicatorResult => Task.FromResult(_cache.GetOrAdd(BuildCacheKey(subscriptionId), factory));

  public Task InvalidateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
  {
    _cache.Remove(BuildCacheKey(subscriptionId));
    return Task.CompletedTask;
  }

  private static string BuildCacheKey(Guid subscriptionId) => string.Join(':', subscriptionId.ToString(), "indicator");
}