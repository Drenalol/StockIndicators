using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Aroon;

public class AroonIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<AroonIndicatorCommand, AroonIndicatorResult>
{
  public AroonIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<AroonIndicatorResult> Handle(AroonIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<AroonIndicatorTemplate>(request);

    var aroonResult = request.Candles
      .ToAroon(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new AroonIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevUp = indicatorResult.AroonUp;
    var prevDown = indicatorResult.AroonDown;

    indicatorResult.AroonUp = aroonResult.AroonUp.GetValueOrDefault();
    indicatorResult.AroonDown = aroonResult.AroonDown.GetValueOrDefault();
    indicatorResult.Oscillator = aroonResult.Oscillator.GetValueOrDefault();

    var triggered = false;
    var trend = AroonTrend.None;

    // Up/Down crossover: the direction whose extreme is the fresher one takes the lead.
    if (HasPreviousState())
    {
      var wasBelow = prevUp <= prevDown;
      var isNowAbove = indicatorResult.AroonUp > indicatorResult.AroonDown;
      var wasAbove = prevUp >= prevDown;
      var isNowBelow = indicatorResult.AroonUp < indicatorResult.AroonDown;

      if (wasBelow && isNowAbove)
        trend = AroonTrend.BullishCross;
      else if (wasAbove && isNowBelow)
        trend = AroonTrend.BearishCross;

      triggered = trend != AroonTrend.None;
    }

    indicatorResult.Trend = trend;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prevUp != 0 || prevDown != 0;
  }
}
