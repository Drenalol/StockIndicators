using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Stochastic;

public class StochasticIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<StochasticIndicatorCommand, StochasticIndicatorResult>
{
  public StochasticIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<StochasticIndicatorResult> Handle(StochasticIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<StochasticIndicatorTemplate>(request);

    var stochResult = request.Candles
      .ToStoch(template.Period, template.SignalPeriod, template.SmoothPeriod)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new StochasticIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevPercK = indicatorResult.PercK;
    var prevPercD = indicatorResult.PercD;

    indicatorResult.PercD = stochResult.D.GetValueOrDefault();
    indicatorResult.PercK = stochResult.K.GetValueOrDefault();

    var triggered = false;
    var trend = StochTrend.None;

    if (indicatorResult.PercK >= template.Overbought)
      trend = StochTrend.Overbought;
    else if (indicatorResult.PercK <= template.Oversold)
      trend = StochTrend.Oversold;

    if (HasPreviousState())
    {
      // Classic zone gating: a bullish golden cross must leave the oversold zone and a
      // bearish death cross the overbought zone; mid-range crossings are noise. Setting
      // the zones to 0/100 accepts every crossover (no zone filter).
      var wasBelowD = prevPercK <= prevPercD + prevPercD * template.Deviation;
      var isAboveD = indicatorResult.PercK - indicatorResult.PercK * template.Deviation > indicatorResult.PercD;
      var wasAboveD = prevPercK - prevPercK * template.Deviation > prevPercD;
      var isBelowD = indicatorResult.PercD - indicatorResult.PercD * template.Deviation > indicatorResult.PercK;

      if (wasBelowD && isAboveD && prevPercK <= template.Oversold)
        trend = StochTrend.BullishCross;
      else if (wasAboveD && isBelowD && prevPercK >= template.Overbought)
        trend = StochTrend.BearishCross;

      if (trend == StochTrend.BullishCross || trend == StochTrend.BearishCross)
        triggered = true;
    }

    indicatorResult.PercKPrevious = prevPercK;
    indicatorResult.PercDPrevious = prevPercD;
    indicatorResult.Trend = trend;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prevPercK != 0 || prevPercD != 0;
  }
}
