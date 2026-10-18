using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Macd;

public class MacdIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<MacdIndicatorCommand, MacdIndicatorResult>
{
  public MacdIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MacdIndicatorResult> Handle(MacdIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<MacdIndicatorTemplate>(request);

    var macdResult = request.Candles
      .ToMacd(template.FastPeriods, template.SlowPeriods, template.SignalPeriods)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new MacdIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevHistogram = indicatorResult.Histogram;
    var prevSignal = indicatorResult.Signal;

    indicatorResult.Macd = macdResult.Macd.GetValueOrDefault();
    indicatorResult.Signal = macdResult.Signal.GetValueOrDefault();
    indicatorResult.Histogram = macdResult.Histogram.GetValueOrDefault();

    var triggered = false;
    var trend = MacdTrend.None;

    if (HasPreviousState())
    {
      // Signal crossover: MACD crosses above/below Signal line
      var wasBelowSignal = indicatorResult.Macd <= prevSignal;
      var isNowAboveSignal = indicatorResult.Macd > indicatorResult.Signal;
      var wasAboveSignal = indicatorResult.Macd >= prevSignal;
      var isNowBelowSignal = indicatorResult.Macd < indicatorResult.Signal;

      AlertDirection? eventDirection = null;

      if (wasBelowSignal && isNowAboveSignal)
      {
        trend = MacdTrend.Bullish;
        eventDirection = AlertDirection.Up;
      }
      else if (wasAboveSignal && isNowBelowSignal)
      {
        trend = MacdTrend.Bearish;
        eventDirection = AlertDirection.Down;
      }

      // Histogram sign flip (secondary signal)
      if (eventDirection is null && prevHistogram * indicatorResult.Histogram < 0)
      {
        trend = indicatorResult.Histogram > 0 ? MacdTrend.Bullish : MacdTrend.Bearish;
        eventDirection = indicatorResult.Histogram > 0 ? AlertDirection.Up : AlertDirection.Down;
      }

      // The Direction filter splits bullish and bearish crossovers; Both keeps the
      // historical behavior (the default for stored rules written before Direction existed).
      triggered = eventDirection is { } direction && template.Direction.Matches(direction);
    }

    indicatorResult.Trend = trend;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prevSignal != 0 || prevHistogram != 0;
  }
}
