using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Mfi;

public class MfiIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<MfiIndicatorCommand, MfiIndicatorResult>
{
  public MfiIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MfiIndicatorResult> Handle(MfiIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<MfiIndicatorTemplate>(request);

    var mfiResult = request.Candles
      .ToMfi(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new MfiIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prev = indicatorResult.Mfi;
    indicatorResult.Mfi = mfiResult.Mfi.GetValueOrDefault();

    var triggered = false;
    var state = ZoneState.Neutral;

    if (indicatorResult.Mfi >= template.Overbought)
      state = ZoneState.Overbought;
    else if (indicatorResult.Mfi <= template.Oversold)
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
