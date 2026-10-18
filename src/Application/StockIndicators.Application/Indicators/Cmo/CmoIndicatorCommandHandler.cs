using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Cmo;

public class CmoIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<CmoIndicatorCommand, CmoIndicatorResult>
{
  public CmoIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<CmoIndicatorResult> Handle(CmoIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<CmoIndicatorTemplate>(request);

    var cmoResult = request.Candles
      .ToCmo(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new CmoIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prev = indicatorResult.Cmo;
    indicatorResult.Cmo = cmoResult.Cmo.GetValueOrDefault();

    var triggered = false;
    var state = ZoneState.Neutral;

    if (indicatorResult.Cmo >= template.Overbought)
      state = ZoneState.Overbought;
    else if (indicatorResult.Cmo <= template.Oversold)
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
