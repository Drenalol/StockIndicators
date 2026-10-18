using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Adx;

public class AdxIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<AdxIndicatorCommand, AdxIndicatorResult>
{
  public AdxIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<AdxIndicatorResult> Handle(AdxIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<AdxIndicatorTemplate>(request);

    var adxResult = request.Candles
      .ToAdx(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new AdxIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevAdx = indicatorResult.Adx;

    indicatorResult.Adx = adxResult.Adx.GetValueOrDefault();
    indicatorResult.DmPlus = adxResult.Pdi.GetValueOrDefault();
    indicatorResult.DmMinus = adxResult.Mdi.GetValueOrDefault();

    var strength = GetStrength(indicatorResult.Adx);
    indicatorResult.Strength = strength;

    var triggered = false;

    if (HasPreviousState())
    {
      var crossedUp = prevAdx < template.AdxThreshold && indicatorResult.Adx >= template.AdxThreshold;
      var crossedDown = prevAdx >= template.AdxThreshold && indicatorResult.Adx < template.AdxThreshold;

      if (crossedUp || crossedDown)
        triggered = true;
    }

    return Set(indicatorResult, triggered);

    static AdxStrength GetStrength(double adx) =>
      adx switch
      {
        >= 50 => AdxStrength.VeryStrong,
        >= 25 => AdxStrength.Strong,
        >= 20 => AdxStrength.Moderate,
        _ => AdxStrength.Weak
      };

    bool HasPreviousState() => prevAdx != 0;
  }
}
