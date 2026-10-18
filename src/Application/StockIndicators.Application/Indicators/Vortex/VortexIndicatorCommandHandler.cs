using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Vortex;

public class VortexIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<VortexIndicatorCommand, VortexIndicatorResult>
{
  public VortexIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<VortexIndicatorResult> Handle(VortexIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<VortexIndicatorTemplate>(request);

    var vortexResult = request.Candles
      .ToVortex(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new VortexIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevPvi = indicatorResult.Pvi;
    var prevNvi = indicatorResult.Nvi;

    indicatorResult.Pvi = vortexResult.Pvi.GetValueOrDefault();
    indicatorResult.Nvi = vortexResult.Nvi.GetValueOrDefault();

    var triggered = false;
    var trend = VortexTrend.None;

    // VI+/VI- crossover: whichever movement line takes the lead marks the dominant direction.
    if (HasPreviousState())
    {
      var wasBelow = prevPvi <= prevNvi;
      var isNowAbove = indicatorResult.Pvi > indicatorResult.Nvi;
      var wasAbove = prevPvi >= prevNvi;
      var isNowBelow = indicatorResult.Pvi < indicatorResult.Nvi;

      if (wasBelow && isNowAbove)
        trend = VortexTrend.BullishCross;
      else if (wasAbove && isNowBelow)
        trend = VortexTrend.BearishCross;

      triggered = trend != VortexTrend.None;
    }

    indicatorResult.Trend = trend;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prevPvi != 0 || prevNvi != 0;
  }
}
