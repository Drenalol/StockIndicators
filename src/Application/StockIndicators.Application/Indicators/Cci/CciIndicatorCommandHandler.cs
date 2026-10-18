using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Cci;

public class CciIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<CciIndicatorCommand, CciIndicatorResult>
{
  public CciIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<CciIndicatorResult> Handle(CciIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<CciIndicatorTemplate>(request);

    var cciResult = request.Candles
      .ToCci(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new CciIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prev = indicatorResult.Cci;
    indicatorResult.Cci = cciResult.Cci.GetValueOrDefault();

    var triggered = false;
    var state = ZoneState.Neutral;

    if (indicatorResult.Cci >= template.Overbought)
      state = ZoneState.Overbought;
    else if (indicatorResult.Cci <= template.Oversold)
      state = ZoneState.Oversold;

    // Trigger when the zone is entered or left, mirroring the RSI handler.
    if (HasPreviousState())
    {
      var prevWasOverbought = prev >= template.Overbought;
      var prevWasOversold = prev <= template.Oversold;

      if (prevWasOverbought && state != ZoneState.Overbought)
      {
        triggered = true;
      }
      else if (prevWasOversold && state != ZoneState.Oversold)
      {
        triggered = true;
      }
      else if (!prevWasOverbought && !prevWasOversold && (state == ZoneState.Overbought || state == ZoneState.Oversold))
      {
        triggered = true;
      }
    }

    indicatorResult.State = state;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prev != 0;
  }
}
