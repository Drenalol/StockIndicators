using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Atr;

public class AtrIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<AtrIndicatorCommand, AtrIndicatorResult>
{
  public AtrIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<AtrIndicatorResult> Handle(AtrIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<AtrIndicatorTemplate>(request);

    var atrResult = request.Candles
      .ToAtr(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new AtrIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var price = (double)request.Candles[^1].Close;
    var atr = atrResult.Atr.GetValueOrDefault();
    var tr = atrResult.Tr.GetValueOrDefault();

    indicatorResult.TrueRange = tr;
    indicatorResult.Atr = atr;
    indicatorResult.AtrPercent = price > 0 ? atr / price * 100 : 0;

    var triggered = indicatorResult.AtrPercent >= template.MinPercent;

    return Set(indicatorResult, triggered);
  }
}
