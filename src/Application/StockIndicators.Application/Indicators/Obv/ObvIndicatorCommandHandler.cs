using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Obv;

public class ObvIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<ObvIndicatorCommand, ObvIndicatorResult>
{
  public ObvIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<ObvIndicatorResult> Handle(ObvIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<ObvIndicatorTemplate>(request);

    var obvResult = request.Candles
      .ToObv()
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new ObvIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var prevObv = indicatorResult.Obv;
    var prevEma = indicatorResult.ObvEma;

    indicatorResult.Obv = obvResult.Obv;

    var obvValues = AccumulateObv(request.Candles);
    indicatorResult.ObvEma = ComputeEma(obvValues, template.EmaPeriod);

    var trend = ObvTrend.None;
    var triggered = false;

    if (HasPreviousState())
    {
      var wasBelowEma = prevObv <= prevEma;
      var isAboveEma = indicatorResult.Obv > indicatorResult.ObvEma;
      var wasAboveEma = prevObv >= prevEma;
      var isBelowEma = indicatorResult.Obv < indicatorResult.ObvEma;

      if (wasBelowEma && isAboveEma)
      {
        trend = ObvTrend.Bullish;
        triggered = true;
      }
      else if (wasAboveEma && isBelowEma)
      {
        trend = ObvTrend.Bearish;
        triggered = true;
      }
    }

    indicatorResult.Trend = trend;

    return Set(indicatorResult, triggered);

    bool HasPreviousState() => prevObv != 0 || prevEma != 0;

    static IList<double> AccumulateObv(Domain.Models.Candles.Candle[] candles)
    {
      var result = new double[candles.Length];
      double runningObv = 0;
      double? prevClose = null;

      for (var i = 0; i < candles.Length; i++)
      {
        if (prevClose.HasValue)
        {
          var close = (double)candles[i].Close;
          if (close > prevClose.Value)
            runningObv += (double)candles[i].Volume;
          else if (close < prevClose.Value)
            runningObv -= (double)candles[i].Volume;
        }
        result[i] = runningObv;
        prevClose = (double)candles[i].Close;
      }

      return result;
    }

    static double ComputeEma(IList<double> values, int period)
    {
      if (values.Count < period)
        return values.Count > 0 ? values[^1] : 0;

      var k = 2.0 / (period + 1);
      double ema = values.Take(period).Average();

      for (var i = period; i < values.Count; i++)
        ema = ema + k * (values[i] - ema);

      return ema;
    }
  }
}
