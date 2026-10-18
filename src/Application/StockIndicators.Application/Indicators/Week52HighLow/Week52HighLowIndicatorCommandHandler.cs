using Serilog;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Week52HighLow;

/// <summary>
/// 52-week extremes computed over the daily-candle cache: a new extreme fires when the close
/// steps over the previous window's highest high / lowest low, a pullback when the close has
/// retreated from the extreme by the configured percent. Zone-entry latch (the PriceChange
/// pattern): entering the zone fires once, re-arming happens when the close leaves it.
/// The window is calendar-anchored on the last candle and always excludes it; while the cache
/// does not yet cover ~90% of the window the condition silently stays unmet (a young series
/// would report a bogus "new high" from a handful of candles).
/// </summary>
public class Week52HighLowIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<Week52HighLowIndicatorCommand, Week52HighLowIndicatorResult>
{
  // Fraction of the calendar window the cache must cover before extremes are trustworthy
  private const double WindowCoverageRatio = 0.9;

  public Week52HighLowIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<Week52HighLowIndicatorResult> Handle(Week52HighLowIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<Week52HighLowIndicatorTemplate>(request);
    var candles = request.Candles;

    if (candles.Length < 2)
      return False(new Week52HighLowIndicatorResult());

    var last = candles[^1];
    var windowFrom = last.Timestamp - TimeSpan.FromDays(template.Weeks * 7);
    var coverageFrom = last.Timestamp - TimeSpan.FromDays(template.Weeks * 7 * WindowCoverageRatio);

    // The candles arrive oldest -> newest; the window is everything before the last candle
    // that still lies inside the calendar range, so window[^1] is its oldest member.
    var window = new List<Candle>(candles.Length);

    for (var i = candles.Length - 2; i >= 0; i--)
      if (candles[i].Timestamp >= windowFrom)
        window.Add(candles[i]);

    if (window.Count == 0 || window[^1].Timestamp > coverageFrom)
      return False(new Week52HighLowIndicatorResult());

    var close = (double)last.Close;

    double? windowHigh = null;
    double? windowLow = null;

    if (template.Extreme is Week52Extreme.Both or Week52Extreme.High)
    {
      var high = 0d;
      foreach (var candle in window)
        high = Math.Max(high, (double)candle.High);

      if (high > 0)
        windowHigh = high;
    }

    if (template.Extreme is Week52Extreme.Both or Week52Extreme.Low)
    {
      var low = double.MaxValue;
      foreach (var candle in window)
        low = Math.Min(low, (double)candle.Low);

      if (low > 0 && low < double.MaxValue)
        windowLow = low;
    }

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new Week52HighLowIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var highZone = false;
    var lowZone = false;
    double highValue = 0;
    double lowValue = 0;

    if (windowHigh is { } windowHighValue)
    {
      if (template.Mode == Week52Mode.NewExtreme)
      {
        highZone = close > windowHighValue;
        highValue = (close / windowHighValue - 1) * 100;
      }
      else
      {
        var retreat = (windowHighValue - close) / windowHighValue * 100;
        highZone = retreat >= template.PullbackPercent;
        highValue = -retreat;
      }
    }

    if (windowLow is { } windowLowValue)
    {
      if (template.Mode == Week52Mode.NewExtreme)
      {
        lowZone = close < windowLowValue;
        lowValue = (close / windowLowValue - 1) * 100;
      }
      else
      {
        var bounce = (close - windowLowValue) / windowLowValue * 100;
        lowZone = bounce >= template.PullbackPercent;
        lowValue = bounce;
      }
    }

    var firesHigh = highZone && !indicatorResult.HighActive;
    var firesLow = lowZone && !indicatorResult.LowActive;

    indicatorResult.HighActive = highZone;
    indicatorResult.LowActive = lowZone;

    var triggered = firesHigh || firesLow;

    if (triggered)
    {
      indicatorResult.Value = firesHigh ? highValue : lowValue;
      indicatorResult.Kind = (template.Mode, firesHigh) switch
      {
        (Week52Mode.NewExtreme, true) => Week52AlertKind.NewHigh,
        (Week52Mode.NewExtreme, false) => Week52AlertKind.NewLow,
        (Week52Mode.Pullback, true) => Week52AlertKind.PullbackFromHigh,
        _ => Week52AlertKind.BounceFromLow,
      };
    }
    else
      indicatorResult.Kind = Week52AlertKind.None;

    return Set(indicatorResult, triggered);
  }
}
