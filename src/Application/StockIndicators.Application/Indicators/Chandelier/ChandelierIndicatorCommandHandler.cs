using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Chandelier;

public class ChandelierIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<ChandelierIndicatorCommand, ChandelierIndicatorResult>
{
  public ChandelierIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<ChandelierIndicatorResult> Handle(ChandelierIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<ChandelierIndicatorTemplate>(request);

    var type = template.Type == ChandelierDirection.Long ? Direction.Long : Direction.Short;

    var exitResult = request.Candles
      .ToChandelier(template.Period, template.Multiplier, type)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new ChandelierIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevSide = indicatorResult.Side;

    indicatorResult.Line = exitResult.ChandelierExit.GetValueOrDefault();

    var price = (double)request.Candles[^1].Close;
    indicatorResult.Side = indicatorResult.Line == 0
      ? ChandelierSide.None
      : price >= indicatorResult.Line ? ChandelierSide.Above : ChandelierSide.Below;

    // Trigger when the close crosses the stop line: an exit signal for the tracked position.
    var triggered = prevSide != ChandelierSide.None && indicatorResult.Side != ChandelierSide.None
      && prevSide != indicatorResult.Side;

    return Set(indicatorResult, triggered);
  }
}
