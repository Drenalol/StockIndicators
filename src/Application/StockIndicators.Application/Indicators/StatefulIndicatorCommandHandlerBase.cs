using Serilog;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators;

public abstract class StatefulIndicatorCommandHandlerBase<TCommand, TResult> : IndicatorCommandHandlerBase<TCommand, TResult> where TCommand : IndicatorCommandBase<TResult> where TResult : IndicatorResult, new()
{
  private readonly IIndicatorCache _indicatorCache;

  protected StatefulIndicatorCommandHandlerBase(IIndicatorCache indicatorCache, ILogger logger) : base(logger) => _indicatorCache = indicatorCache;

  protected Task<TResult> GetOrAddCacheResultAsync(TCommand command, Func<TResult> factory, CancellationToken cancellationToken = default) => _indicatorCache.GetOrAddAsync(command.SubscriptionId, factory, cancellationToken);
}