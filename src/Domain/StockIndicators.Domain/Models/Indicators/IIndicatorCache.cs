namespace StockIndicators.Domain.Models.Indicators;

public interface IIndicatorCache
{
  Task<T> GetOrAddAsync<T>(Guid subscriptionId, Func<T> factory, CancellationToken cancellationToken = default) where T : IndicatorResult;
  Task InvalidateAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}