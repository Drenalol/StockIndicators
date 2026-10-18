using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Rsi;

public class RsiIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<RsiIndicatorCommand, RsiIndicatorResult>
{
  public RsiIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<RsiIndicatorResult> Handle(RsiIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<RsiIndicatorTemplate>(request);

    var rsiResult = request.Candles
      .ToRsi(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new RsiIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevRsi = indicatorResult.Rsi;
    indicatorResult.Rsi = rsiResult.Rsi.GetValueOrDefault();

    var triggered = false;
    var state = RsiState.Neutral;

    if (indicatorResult.Rsi >= template.Overbought)
      state = RsiState.Overbought;
    else if (indicatorResult.Rsi <= template.Oversold)
      state = RsiState.Oversold;

    // Trigger on state transition. The Direction filter splits the two zones: Up watches
    // the overbought level crossings, Down the oversold ones, Both keeps the historical
    // "either zone" behavior (the default for stored rules written before Direction existed).
    if (HasPreviousState())
      triggered = GetEventDirection() is { } eventDirection && template.Direction.Matches(eventDirection);

    indicatorResult.State = state;

    return Set(indicatorResult, triggered);

    AlertDirection? GetEventDirection()
    {
      var prevWasOverbought = prevRsi >= template.Overbought;
      var prevWasOversold = prevRsi <= template.Oversold;

      // Leaving the overbought zone is its event, leaving the oversold zone is that one's;
      // entering a zone from neutral belongs to the zone being entered.
      if (prevWasOverbought && state != RsiState.Overbought)
        return AlertDirection.Up;
      if (prevWasOversold && state != RsiState.Oversold)
        return AlertDirection.Down;
      if (!prevWasOverbought && !prevWasOversold && state == RsiState.Overbought)
        return AlertDirection.Up;
      if (!prevWasOverbought && !prevWasOversold && state == RsiState.Oversold)
        return AlertDirection.Down;

      return null;
    }

    bool HasPreviousState() => prevRsi != 0;
  }
}
