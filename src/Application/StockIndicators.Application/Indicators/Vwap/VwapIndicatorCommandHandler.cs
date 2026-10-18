using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Vwap;

public class VwapIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<VwapIndicatorCommand, VwapIndicatorResult>
{
  public VwapIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<VwapIndicatorResult> Handle(VwapIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<VwapIndicatorTemplate>(request);

    var vwapResult = request.Candles
      .ToVwap()
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new VwapIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var price = (double)request.Candles[^1].Close;
    var prevPrice = request.Candles.Length > 1 ? (double)request.Candles[^2].Close : price;
    var vwap = vwapResult.Vwap.GetValueOrDefault();

    indicatorResult.Vwap = vwap;

    var currentPosition = GetPosition(price, vwap, template.Deviation);
    var prevPosition = GetPosition(prevPrice, vwap, template.Deviation);

    // Trigger on crossover: price crosses VWAP
    var triggered = prevPosition != currentPosition && prevPosition != VwapPosition.On && currentPosition != VwapPosition.On;

    indicatorResult.Position = currentPosition;

    return Set(indicatorResult, triggered);

    static VwapPosition GetPosition(double price, double vwap, double deviation)
    {
      if (vwap == 0)
        return VwapPosition.On;

      var pctDiff = (price - vwap) / vwap;
      if (Math.Abs(pctDiff) <= deviation)
        return VwapPosition.On;
      return pctDiff > 0 ? VwapPosition.Above : VwapPosition.Below;
    }
  }
}
