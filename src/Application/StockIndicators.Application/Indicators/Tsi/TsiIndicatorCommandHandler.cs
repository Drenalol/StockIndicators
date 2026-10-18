using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Tsi;

public class TsiIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<TsiIndicatorCommand, TsiIndicatorResult>
{
  public TsiIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<TsiIndicatorResult> Handle(TsiIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<TsiIndicatorTemplate>(request);

    var tsiResult = request.Candles
      .ToTsi(template.LongPeriods, template.SignalPeriods)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new TsiIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevTsi = indicatorResult.Tsi;
    var prevSignal = indicatorResult.Signal;

    indicatorResult.Tsi = tsiResult.Tsi.GetValueOrDefault();
    indicatorResult.Signal = tsiResult.Signal.GetValueOrDefault();

    var triggered = false;
    var trend = TsiTrend.None;

    // Signal crossover: the TSI line passes its signal line from one evaluation to the next.
    if (HasPreviousState())
    {
      var wasBelow = prevTsi <= prevSignal;
      var isNowAbove = indicatorResult.Tsi > indicatorResult.Signal;
      var wasAbove = prevTsi >= prevSignal;
      var isNowBelow = indicatorResult.Tsi < indicatorResult.Signal;

      if (wasBelow && isNowAbove)
      {
        trend = TsiTrend.Bullish;
        triggered = true;
      }
      else if (wasAbove && isNowBelow)
      {
        trend = TsiTrend.Bearish;
        triggered = true;
      }
    }

    indicatorResult.Trend = trend;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prevTsi != 0 || prevSignal != 0;
  }
}
