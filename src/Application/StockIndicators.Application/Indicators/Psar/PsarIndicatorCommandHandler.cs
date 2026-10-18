using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Psar;

public class PsarIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<PsarIndicatorCommand, PsarIndicatorResult>
{
  public PsarIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<PsarIndicatorResult> Handle(PsarIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<PsarIndicatorTemplate>(request);

    var sarResult = request.Candles
      .ToParabolicSar(template.AccelerationStep, template.MaxAccelerationFactor)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new PsarIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevDirection = indicatorResult.Direction;

    indicatorResult.Sar = sarResult.Sar.GetValueOrDefault();

    var price = (double)request.Candles[^1].Close;
    indicatorResult.Direction = price >= indicatorResult.Sar ? PsarDirection.Bullish : PsarDirection.Bearish;

    // Trigger on the trend reversal: the dots switched sides of the price.
    var hasPrev = prevDirection == PsarDirection.Bullish || prevDirection == PsarDirection.Bearish;
    var triggered = hasPrev && prevDirection != indicatorResult.Direction;

    return Set(indicatorResult, triggered);
  }
}
