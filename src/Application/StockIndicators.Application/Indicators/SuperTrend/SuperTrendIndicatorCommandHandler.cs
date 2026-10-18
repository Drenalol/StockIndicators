using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.SuperTrend;

public class SuperTrendIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<SuperTrendIndicatorCommand, SuperTrendIndicatorResult>
{
  public SuperTrendIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<SuperTrendIndicatorResult> Handle(SuperTrendIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<SuperTrendIndicatorTemplate>(request);

    var stResult = request.Candles
      .ToSuperTrend(template.Period, template.Multiplier)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new SuperTrendIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevDirection = indicatorResult.Direction;

    var superTrendValue = (double)stResult.SuperTrend.GetValueOrDefault();
    indicatorResult.SuperTrend = superTrendValue;
    indicatorResult.UpperBand = (double)stResult.UpperBand.GetValueOrDefault();
    indicatorResult.LowerBand = (double)stResult.LowerBand.GetValueOrDefault();

    var price = (double)request.Candles[^1].Close;
    indicatorResult.Direction = price >= superTrendValue ? SuperTrendDirection.Bullish : SuperTrendDirection.Bearish;

    // Trigger on direction flip
    var hasPrev = prevDirection == SuperTrendDirection.Bullish || prevDirection == SuperTrendDirection.Bearish;
    var triggered = hasPrev && prevDirection != indicatorResult.Direction;

    return Set(indicatorResult, triggered);
  }
}
