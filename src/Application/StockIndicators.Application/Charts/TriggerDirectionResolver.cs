using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Decides whether a trigger marker points up (buy-side signal), down (sell-side signal)
/// or is a directionless event (a neutral dot), so the arrow can never read as the opposite
/// signal from the alert behind it. The decision reuses the handler's own comparison math
/// replayed over the freshly computed chart series on the trigger candle and the one before
/// it - the stateful handlers keep exactly those previous values in their cache slots, so a
/// series-based replay is semantically equal to the cached state (the example-anchor guard
/// test asserts both agree). Returns null only when the decision is impossible (warmup gaps,
/// a trigger time outside the candles) - the client then falls back to its own geometry.
/// </summary>
public static class TriggerDirectionResolver
{
  public static ChartTriggerDirection? Resolve(
    AppIndicator indicator,
    IIndicatorTemplate template,
    Candle[] candles,
    IReadOnlyList<ChartSeries> series,
    DateTime triggerAt)
  {
    var triggerIndex = IndexOfCandle(candles, triggerAt);

    if (triggerIndex < 1)
      return null;

    var candle = candles[triggerIndex];
    var prevCandle = candles[triggerIndex - 1];
    var date = candle.Timestamp;
    var prevDate = prevCandle.Timestamp;

    var at = ValuesAt(series, date);
    var prev = ValuesAt(series, prevDate);

    double? Current(string name) => at.TryGetValue(name, out var v) ? v : null;
    double? Before(string name) => prev.TryGetValue(name, out var v) ? v : null;

    switch (indicator)
    {
      // Directionless events: a threshold/volatility/level crossing carries no bull/bear.
      case AppIndicator.ADX:
      case AppIndicator.ATR:
      case AppIndicator.Volume:
      case AppIndicator.RoundPrice:
        return ChartTriggerDirection.Neutral;

      case AppIndicator.Gap:
        return candle.Open > prevCandle.Close
          ? ChartTriggerDirection.Up
          : candle.Open < prevCandle.Close ? ChartTriggerDirection.Down : null;

      case AppIndicator.SMA:
      case AppIndicator.EMA:
      case AppIndicator.WMA:
      case AppIndicator.HMA:
      {
        var t = (MaIndicatorTemplate)template;
        var lineName = $"{indicator} {t.Periods}";

        return AverageCross(lineName, t.Deviation);
      }

      case AppIndicator.VWMA:
      {
        var t = (VwmaIndicatorTemplate)template;

        return AverageCross($"VWMA {t.Periods}", t.Deviation);
      }

      case AppIndicator.KAMA:
      {
        var t = (KamaIndicatorTemplate)template;

        // The KAMA line is named by its efficiency-ratio period (IndicatorChartBuilder).
        return AverageCross($"KAMA {t.ErPeriods}", t.Deviation);
      }

      case AppIndicator.RSI:
      {
        var t = (RsiIndicatorTemplate)template;
        var cur = Current("RSI");
        var before = Before("RSI");

        if (cur is null || before is null)
          return null;

        // RsiIndicatorCommandHandler state transitions.
        if (before >= t.Overbought && cur < t.Overbought)
          return ChartTriggerDirection.Down;
        if (before <= t.Oversold && cur > t.Oversold)
          return ChartTriggerDirection.Up;
        if (before > t.Oversold && before < t.Overbought && cur >= t.Overbought)
          return ChartTriggerDirection.Up;
        if (before > t.Oversold && before < t.Overbought && cur <= t.Oversold)
          return ChartTriggerDirection.Down;

        return null;
      }

      case AppIndicator.MACD:
      {
        var macd = Current("MACD");
        var signal = Current("Signal");
        var signalPrev = Before("Signal");
        var histogram = Current("Histogram");
        var histogramPrev = Before("Histogram");

        // MacdIndicatorCommandHandler: current MACD vs the previous signal, then the histogram flip.
        if (macd is not null && signal is not null && signalPrev is not null)
        {
          if (macd <= signalPrev && macd > signal)
            return ChartTriggerDirection.Up;
          if (macd >= signalPrev && macd < signal)
            return ChartTriggerDirection.Down;
        }

        if (histogram is not null && histogramPrev is not null && histogramPrev.Value * histogram.Value < 0)
          return histogram > 0 ? ChartTriggerDirection.Up : ChartTriggerDirection.Down;

        return null;
      }

      case AppIndicator.BollingerBands:
      {
        var t = (BollingerBandsIndicatorTemplate)template;
        var upper = Current("BB Upper");
        var lower = Current("BB Lower");

        if (upper is null || lower is null)
          return null;

        // BollingerBandsIndicatorCommandHandler: both positions are measured against the
        // current bands (not each candle's own), the signal is an inside/outside transition.
        var part = PartPrice(t.CandlePart);
        var before = Position(part(prevCandle), upper.Value, lower.Value);
        var now = Position(part(candle), upper.Value, lower.Value);

        if (before == 0 && now == 1)
          return ChartTriggerDirection.Up;
        if (before == 0 && now == -1)
          return ChartTriggerDirection.Down;
        if (before != 0 && now == 0)
          return before == 1 ? ChartTriggerDirection.Down : ChartTriggerDirection.Up;

        return null;

        static Func<Candle, double> PartPrice(Domain.Models.Indicators.CandlePart part) =>
          part switch
          {
            Domain.Models.Indicators.CandlePart.Open => c => (double)c.Open,
            Domain.Models.Indicators.CandlePart.High => c => (double)c.High,
            Domain.Models.Indicators.CandlePart.Low => c => (double)c.Low,
            _ => c => (double)c.Close,
          };

        static int Position(double price, double upperBand, double lowerBand)
        {
          if (price > upperBand)
            return 1;
          if (price < lowerBand)
            return -1;
          if (upperBand > 0 && Math.Abs(price - upperBand) / upperBand < 0.001)
            return 1;
          if (lowerBand > 0 && Math.Abs(price - lowerBand) / lowerBand < 0.001)
            return -1;
          return 0;
        }
      }

      case AppIndicator.SuperTrend:
      {
        var line = Current("SuperTrend");
        var linePrev = Before("SuperTrend");

        if (line is null || linePrev is null)
          return null;

        // SuperTrendIndicatorCommandHandler: direction = close >= line, the signal is a flip.
        var now = (double)candle.Close >= line.Value;
        var before = (double)prevCandle.Close >= linePrev.Value;

        return !before && now ? ChartTriggerDirection.Up : before && !now ? ChartTriggerDirection.Down : null;
      }

      case AppIndicator.Ichimoku:
      {
        var tenkan = Current("Tenkan");
        var kijun = Current("Kijun");
        var tenkanPrev = Before("Tenkan");
        var kijunPrev = Before("Kijun");

        if (tenkan is null || kijun is null || tenkanPrev is null || kijunPrev is null)
          return null;

        // IchimokuIndicatorCommandHandler: a Tenkan/Kijun cross.
        if (tenkanPrev <= kijunPrev && tenkan > kijun)
          return ChartTriggerDirection.Up;
        if (tenkanPrev >= kijunPrev && tenkan < kijun)
          return ChartTriggerDirection.Down;

        return null;
      }

      case AppIndicator.VWAP:
      {
        var t = (VwapIndicatorTemplate)template;
        var vwap = Current("VWAP");
        var vwapPrev = Before("VWAP");

        if (vwap is null || vwapPrev is null)
          return null;

        // VwapIndicatorCommandHandler: close position vs VWAP leaving the deviation band.
        var before = Position((double)prevCandle.Close, vwapPrev.Value, t.Deviation);
        var now = Position((double)candle.Close, vwap.Value, t.Deviation);

        return before == -1 && now == 1
          ? ChartTriggerDirection.Up
          : before == 1 && now == -1 ? ChartTriggerDirection.Down : null;

        static int Position(double price, double lineValue, double deviation)
        {
          var pct = (price - lineValue) / lineValue;
          return Math.Abs(pct) <= deviation ? 0 : pct > 0 ? 1 : -1;
        }
      }

      case AppIndicator.OBV:
      {
        var obv = Current("OBV");
        var ema = Current("OBV EMA");
        var obvPrev = Before("OBV");
        var emaPrev = Before("OBV EMA");

        if (obv is null || ema is null || obvPrev is null || emaPrev is null)
          return null;

        // ObvIndicatorCommandHandler: OBV crossing its EMA.
        if (obvPrev <= emaPrev && obv > ema)
          return ChartTriggerDirection.Up;
        if (obvPrev >= emaPrev && obv < ema)
          return ChartTriggerDirection.Down;

        return null;
      }

      case AppIndicator.Stochastic:
      {
        var t = (StochasticIndicatorTemplate)template;
        return StochCross(at, prev, t.Deviation, t.Oversold, t.Overbought);
      }

      case AppIndicator.StochRSI:
      {
        var t = (StochRsiIndicatorTemplate)template;
        return StochCross(at, prev, t.Deviation, (double)t.MinChannel, (double)t.MaxChannel);
      }

      case AppIndicator.CCI:
        return ZoneTransition("CCI", ((CciIndicatorTemplate)template).Overbought, ((CciIndicatorTemplate)template).Oversold);

      case AppIndicator.MFI:
        return ZoneTransition("MFI", ((MfiIndicatorTemplate)template).Overbought, ((MfiIndicatorTemplate)template).Oversold);

      case AppIndicator.WilliamsR:
        return ZoneTransition("Williams %R", ((WilliamsRIndicatorTemplate)template).Overbought, ((WilliamsRIndicatorTemplate)template).Oversold);

      case AppIndicator.CMO:
        return ZoneTransition("CMO", ((CmoIndicatorTemplate)template).Overbought, ((CmoIndicatorTemplate)template).Oversold);

      case AppIndicator.TSI:
      {
        var tsi = Current("TSI");
        var tsiPrev = Before("TSI");
        var signal = Current("Signal");
        var signalPrev = Before("Signal");

        if (tsi is null || tsiPrev is null || signal is null || signalPrev is null)
          return null;

        // TsiIndicatorCommandHandler: the TSI line crossing its signal line.
        if (tsiPrev <= signalPrev && tsi > signal)
          return ChartTriggerDirection.Up;
        if (tsiPrev >= signalPrev && tsi < signal)
          return ChartTriggerDirection.Down;

        return null;
      }

      case AppIndicator.Keltner:
        return ChannelTransition("Keltner Upper", "Keltner Lower");

      case AppIndicator.Donchian:
        return ChannelTransition("Donchian Upper", "Donchian Lower");

      case AppIndicator.PSAR:
      {
        var sar = Current("PSAR");
        var sarPrev = Before("PSAR");

        if (sar is null || sarPrev is null)
          return null;

        // PsarIndicatorCommandHandler: direction = close >= sar, the signal is the dots flipping sides.
        var now = (double)candle.Close >= sar.Value;
        var before = (double)prevCandle.Close >= sarPrev.Value;

        return !before && now ? ChartTriggerDirection.Up : before && !now ? ChartTriggerDirection.Down : null;
      }

      case AppIndicator.Chandelier:
      {
        var line = Current("Chandelier");
        var linePrev = Before("Chandelier");

        if (line is null || linePrev is null)
          return null;

        // ChandelierIndicatorCommandHandler: the close crossing the stop line (above it - a long survives).
        var now = (double)candle.Close >= line.Value;
        var before = (double)prevCandle.Close >= linePrev.Value;

        return !before && now ? ChartTriggerDirection.Up : before && !now ? ChartTriggerDirection.Down : null;
      }

      case AppIndicator.Aroon:
        return CrossOver("Aroon Up", "Aroon Down");

      case AppIndicator.Vortex:
        return CrossOver("VI+", "VI-");

      case AppIndicator.PriceChange:
      {
        var change = Current("Change %");

        if (change is null)
          return null;

        return change >= 0 ? ChartTriggerDirection.Up : ChartTriggerDirection.Down;
      }

      case AppIndicator.OpenChange:
      {
        var change = Current("Open %");

        if (change is null)
          return null;

        // OpenChangeIndicatorCommandHandler fires on the zone entry; the marker points the way
        // the open-to-close move went.
        return change >= 0 ? ChartTriggerDirection.Up : ChartTriggerDirection.Down;
      }

      case AppIndicator.Week52HighLow:
      {
        var t = (Week52HighLowIndicatorTemplate)template;
        var close = (double)candle.Close;

        if (t.Extreme is not Week52Extreme.Low && Current($"{t.Weeks}w High") is { } high && high > 0)
        {
          if (t.Mode == Week52Mode.NewExtreme)
          {
            if (close > high)
              return ChartTriggerDirection.Up;
          }
          else if ((high - close) / high * 100 >= t.PullbackPercent)
            return ChartTriggerDirection.Down;
        }

        if (t.Extreme is not Week52Extreme.High && Current($"{t.Weeks}w Low") is { } low && low > 0)
        {
          if (t.Mode == Week52Mode.NewExtreme)
          {
            if (close < low)
              return ChartTriggerDirection.Down;
          }
          else if ((close - low) / low * 100 >= t.PullbackPercent)
            return ChartTriggerDirection.Up;
        }

        return null;
      }

      case AppIndicator.TrailingStop:
      {
        var t = (TrailingStopIndicatorTemplate)template;
        var line = Current("Trailing Stop");
        var linePrev = Before("Trailing Stop");

        if (line is null || linePrev is null)
          return null;

        // TrailingStopIndicatorCommandHandler: only the crossing into the unsafe side alerts -
        // a long stop hit reads as a sell (down), a short stop hit as a buy (up).
        var now = (double)candle.Close >= line.Value;
        var before = (double)prevCandle.Close >= linePrev.Value;

        if (t.Type == ChandelierDirection.Long)
          return before && !now ? ChartTriggerDirection.Down : null;

        return !before && now ? ChartTriggerDirection.Up : null;
      }

      default:
        return null;
    }

    // The %K/%D crossover semantics of both stochastic handlers (identical comparison math,
    // zone thresholds from the template): bullish when %K rose through %D beyond the
    // tolerance while leaving the lower zone, bearish mirrored at the upper zone.
    ChartTriggerDirection? StochCross(
      IReadOnlyDictionary<string, double> now,
      IReadOnlyDictionary<string, double> before,
      double deviation,
      double zoneLow,
      double zoneHigh)
    {
      if (!now.TryGetValue("%K", out var k) || !now.TryGetValue("%D", out var d)
        || !before.TryGetValue("%K", out var kPrev) || !before.TryGetValue("%D", out var dPrev))
        return null;

      var bullish = kPrev <= dPrev + dPrev * deviation
        && k - k * deviation > d
        && kPrev <= zoneLow;
      var bearish = kPrev - kPrev * deviation > dPrev
        && d - d * deviation > k
        && kPrev >= zoneHigh;

      return bullish ? ChartTriggerDirection.Up : bearish ? ChartTriggerDirection.Down : null;
    }

    // MovingAverageCommandHandlerBase: an upward cross opens below the average and closes
    // within the deviation band above it; a downward cross mirrors that.
    ChartTriggerDirection? AverageCross(string lineName, double deviation)
    {
      var line = Current(lineName);

      if (line is null)
        return null;

      var modulo = (double)candle.Close - line.Value;

      var up = modulo >= 0 && modulo <= deviation * line.Value && (double)candle.Open <= line.Value;
      var down = modulo <= 0 && modulo >= -deviation * line.Value && (double)candle.Open >= line.Value;

      return up ? ChartTriggerDirection.Up : down ? ChartTriggerDirection.Down : null;
    }

    // The zone-transition semantics of the RSI-shaped handlers (CCI, MFI, Williams %R, CMO):
    // a trigger is entering or leaving the overbought/oversold zone; leaving overbought or
    // entering oversold reads as a down signal, the mirrored moves as an up one.
    ChartTriggerDirection? ZoneTransition(string seriesName, double overbought, double oversold)
    {
      var cur = Current(seriesName);
      var before = Before(seriesName);

      if (cur is null || before is null)
        return null;

      if (before >= overbought && cur < overbought)
        return ChartTriggerDirection.Down;
      if (before <= oversold && cur > oversold)
        return ChartTriggerDirection.Up;
      if (before > oversold && before < overbought && cur >= overbought)
        return ChartTriggerDirection.Up;
      if (before > oversold && before < overbought && cur <= oversold)
        return ChartTriggerDirection.Down;

      return null;
    }

    // The channel semantics of the Keltner/Donchian handlers (like Bollinger, without the
    // touching-band states): both positions are measured against the current bands, the
    // signal is an inside/outside transition.
    ChartTriggerDirection? ChannelTransition(string upperName, string lowerName)
    {
      var upper = Current(upperName);
      var lower = Current(lowerName);

      if (upper is null || lower is null)
        return null;

      var before = Position((double)prevCandle.Close, upper.Value, lower.Value);
      var now = Position((double)candle.Close, upper.Value, lower.Value);

      if (before == 0 && now == 1)
        return ChartTriggerDirection.Up;
      if (before == 0 && now == -1)
        return ChartTriggerDirection.Down;
      if (before != 0 && now == 0)
        return before == 1 ? ChartTriggerDirection.Down : ChartTriggerDirection.Up;

      return null;

      static int Position(double price, double upperBand, double lowerBand)
      {
        if (price > upperBand)
          return 1;
        if (lowerBand > 0 && price < lowerBand)
          return -1;
        return 0;
      }
    }

    // Two-line crossovers (Aroon Up/Down, VI+/VI-): the first line taking the lead is bullish,
    // losing the lead bearish - the prev-vs-prev comparison both handlers use.
    ChartTriggerDirection? CrossOver(string fastName, string slowName)
    {
      var fast = Current(fastName);
      var slow = Current(slowName);
      var fastPrev = Before(fastName);
      var slowPrev = Before(slowName);

      if (fast is null || slow is null || fastPrev is null || slowPrev is null)
        return null;

      if (fastPrev <= slowPrev && fast > slow)
        return ChartTriggerDirection.Up;
      if (fastPrev >= slowPrev && fast < slow)
        return ChartTriggerDirection.Down;

      return null;
    }
  }

  private static int IndexOfCandle(Candle[] candles, DateTime date)
  {
    var x = ChartX.ToX(date);

    for (var i = candles.Length - 1; i >= 0; i--)
      if (ChartX.ToX(candles[i].Timestamp) == x)
        return i;

    return -1;
  }

  /// <summary>
  /// Name -> value of every series that has a point exactly on the candle's X (warmup-dropped
  /// or clipped points simply miss, and the caller degrades to a null decision).
  /// </summary>
  private static Dictionary<string, double> ValuesAt(IReadOnlyList<ChartSeries> series, DateTime date)
  {
    var x = ChartX.ToX(date);
    var result = new Dictionary<string, double>(series.Count);

    foreach (var s in series)
      for (var i = s.Points.Count - 1; i >= 0; i--)
      {
        if (s.Points[i].X < x)
          break;

        if (s.Points[i].X == x)
        {
          result[s.Name] = s.Points[i].Value;
          break;
        }
      }

    return result;
  }
}
