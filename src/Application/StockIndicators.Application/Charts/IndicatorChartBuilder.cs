using FacioQuo.Stock.Indicators;
using StockIndicators.Application.Indicators;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Converts a computed indicator subscription into chart overlay series for the chart spec.
/// Each indicator maps to a full library series (`.Get*()`), drawn either on the price pane
/// (same scale as candles) or the oscillator pane (secondary axis).
/// </summary>
public static class IndicatorChartBuilder
{
  // Candle colors (match app theme: success / destructive).
  private const string UpColor = "#16a34a";
  private const string DownColor = "#ef4444";

  // Reference-line colors.
  private const string RefRed = "#ef4444";
  private const string RefGreen = "#16a34a";
  private const string RefSlate = "#64748b";

  // Series colors (distinct hues).
  private const string CBlue = "#2563eb";
  private const string CSky = "#0ea5e9";
  private const string CViolet = "#8b5cf6";
  private const string COrange = "#f97316";
  private const string CFuchsia = "#d946ef";
  private const string CGreen = "#10b981";
  private const string CSlate = "#94a3b8";

  // Deviation-band tints (light tints of the line each band belongs to).
  private const string BandBlue = "#93c5fd";
  private const string BandSky = "#7dd3fc";
  private const string BandGreen = "#6ee7b7";
  private const string BandOrange = "#fdba74";
  private const string BandFuchsia = "#f0abfc";

  public record Result(
    IReadOnlyList<ChartSeries> Series,
    bool HasOscillator,
    double? OscMin,
    double? OscMax
  );

  /// <summary>
  /// The hues cycled when merged series would land on a color another series already took.
  /// </summary>
  private static readonly string[] SeriesPalette = [CBlue, CSky, CViolet, COrange, CFuchsia, CGreen, CSlate];

  /// <summary>
  /// Combines the per-indicator results of one instrument into a single chart (the live
  /// snapshot): series are concatenated, the oscillator pane is kept when any indicator
  /// uses it, and its fixed range is used only when every oscillator shares the same one
  /// (0-100 for RSI/Stoch/ADX); a mix of a ranged oscillator with an unbounded one (MACD,
  /// ATR, OBV) falls back to null so the renderer autoscales the right axis. A hue already
  /// taken by another indicator shifts the later series along the palette so two same-kind
  /// indicators (EMA 21 + EMA 50) stay distinguishable; a hue an indicator reuses within
  /// itself (Bollinger's bands share one hue on purpose) is never shifted.
  /// </summary>
  public static Result Merge(IReadOnlyList<Result> results)
  {
    var series = new List<ChartSeries>();
    var colorOwners = new Dictionary<string, int>();

    for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
    {
      foreach (var s in results[resultIndex].Series)
      {
        var color = s.Color;
        var paletteIndex = SeriesPalette.IndexOf(color);
        var shift = 0;

        while (paletteIndex >= 0
          && colorOwners.TryGetValue(color, out var owner)
          && owner != resultIndex
          && shift < SeriesPalette.Length)
        {
          shift++;
          color = SeriesPalette[(paletteIndex + shift) % SeriesPalette.Length];
        }

        colorOwners[color] = resultIndex;
        series.Add(color == s.Color ? s : s with { Color = color });
      }
    }

    var oscillators = results.Where(result => result.HasOscillator).ToArray();
    var ranged = oscillators.Where(result => result.OscMin.HasValue && result.OscMax.HasValue).ToArray();
    var sharedRange = oscillators.Length > 0
      && ranged.Length == oscillators.Length
      && ranged.All(result => result.OscMin == ranged[0].OscMin && result.OscMax == ranged[0].OscMax);

    return new Result(
      series,
      oscillators.Length > 0,
      sharedRange ? ranged[0].OscMin : null,
      sharedRange ? ranged[0].OscMax : null);
  }

  public static Result Build(AppIndicator indicator, IIndicatorTemplate template, Candle[] candles)
  {
    var series = new List<ChartSeries>();
    bool hasOsc = false;
    double? oscMin = null;
    double? oscMax = null;

    switch (indicator)
    {
      case AppIndicator.SMA:
        series.AddRange(BuildMa(candles, (MaIndicatorTemplate)template, "SMA", CBlue, BandBlue));
        break;

      case AppIndicator.EMA:
        series.AddRange(BuildMa(candles, (EmaIndicatorTemplate)template, "EMA", CSky, BandSky));
        break;

      case AppIndicator.VWAP:
      {
        var t = (VwapIndicatorTemplate)template;
        var vwapVals = candles.ToVwap().Select(r => D(r.Vwap)).ToArray();
        series.Add(new ChartSeries("VWAP", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, vwapVals), CFuchsia, []));
        // The trigger zone: price must leave VWAP beyond ±dev (VwapIndicatorCommandHandler).
        series.AddRange(DeviationBand(candles, "VWAP", vwapVals, t.Deviation, BandFuchsia, ChartPane.Price, divideUpper: false));
        break;
      }

      case AppIndicator.BollingerBands:
      {
        var t = (BollingerBandsIndicatorTemplate)template;
        var bb = candles.Use(t.CandlePart.ToCandlePart()).ToBollingerBands(t.Period, t.StandardDeviation);
        series.Add(new ChartSeries("BB Upper", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, bb.Select(r => D(r.UpperBand)).ToArray()), CViolet, []));
        series.Add(new ChartSeries("BB Middle", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, bb.Select(r => D(r.Sma)).ToArray()), RefSlate, []));
        series.Add(new ChartSeries("BB Lower", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, bb.Select(r => D(r.LowerBand)).ToArray()), CViolet, []));
        break;
      }

      case AppIndicator.SuperTrend:
      {
        var t = (SuperTrendIndicatorTemplate)template;
        var st = candles.ToSuperTrend(t.Period, t.Multiplier);
        series.Add(new ChartSeries("SuperTrend", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, st.Select(r => D(r.SuperTrend)).ToArray()), COrange, []));
        break;
      }

      case AppIndicator.Ichimoku:
      {
        var t = (IchimokuIndicatorTemplate)template;
        var ich = candles.ToIchimoku(t.TenkanPeriod, t.KijunPeriod, t.SenkouBPeriod);
        series.Add(new ChartSeries("Tenkan", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, ich.Select(r => D(r.TenkanSen)).ToArray()), CSky, []));
        series.Add(new ChartSeries("Kijun", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, ich.Select(r => D(r.KijunSen)).ToArray()), COrange, []));
        series.Add(new ChartSeries("Chikou", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, ich.Select(r => D(r.ChikouSpan)).ToArray()), RefSlate, []));
        series.Add(new ChartSeries("Span A", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, ich.Select(r => D(r.SenkouSpanA)).ToArray()), CGreen, []));
        series.Add(new ChartSeries("Span B", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, ich.Select(r => D(r.SenkouSpanB)).ToArray()), DownColor, []));
        break;
      }

      case AppIndicator.RSI:
      {
        var t = (RsiIndicatorTemplate)template;
        var rsi = candles.ToRsi(t.Period);
        series.Add(new ChartSeries("RSI", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, rsi.Select(r => D(r.Rsi)).ToArray()), CViolet,
          [new ChartRefLine(t.Overbought, "OB", RefRed), new ChartRefLine(t.Oversold, "OS", RefGreen)]));
        hasOsc = true; oscMin = 0; oscMax = 100;
        break;
      }

      case AppIndicator.Stochastic:
      {
        var t = (StochasticIndicatorTemplate)template;
        var st = candles.ToStoch(t.Period, t.SignalPeriod, t.SmoothPeriod);
        var stD = st.Select(r => D(r.D)).ToArray();
        series.Add(new ChartSeries("%K", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, st.Select(r => D(r.K)).ToArray()), CSky,
          [new ChartRefLine(t.Overbought, "OB", RefRed), new ChartRefLine(t.Oversold, "OS", RefGreen)]));
        series.Add(new ChartSeries("%D", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, stD), COrange, []));
        // The noise zone of the %K/%D crossover: a bullish cross needs %K > %D/(1-dev),
        // a bearish one %K < %D*(1-dev) (StochasticIndicatorCommandHandler).
        series.AddRange(DeviationBand(candles, "%D", stD, t.Deviation, BandOrange, ChartPane.Oscillator, divideUpper: true));
        hasOsc = true; oscMin = 0; oscMax = 100;
        break;
      }

      case AppIndicator.StochRSI:
      {
        var t = (StochRsiIndicatorTemplate)template;
        var sr = candles.ToStochRsi(t.RsiPeriod, t.StochPeriod, t.SignalPeriod, t.SmoothPeriod);
        var srD = sr.Select(r => D(r.Signal)).ToArray();
        series.Add(new ChartSeries("%K", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, sr.Select(r => D(r.StochRsi)).ToArray()), CSky,
          [new ChartRefLine((double)t.MaxChannel, "max", RefRed), new ChartRefLine((double)t.MinChannel, "min", RefGreen)]));
        series.Add(new ChartSeries("%D", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, srD), COrange, []));
        // The noise zone of the %K/%D crossover: a bullish cross needs %K > %D/(1-dev),
        // a bearish one %K < %D*(1-dev) (StochRsiIndicatorCommandHandler).
        series.AddRange(DeviationBand(candles, "%D", srD, t.Deviation, BandOrange, ChartPane.Oscillator, divideUpper: true));
        hasOsc = true; oscMin = 0; oscMax = 100;
        break;
      }

      case AppIndicator.MACD:
      {
        var t = (MacdIndicatorTemplate)template;
        var macd = candles.ToMacd(t.FastPeriods, t.SlowPeriods, t.SignalPeriods);
        series.Add(new ChartSeries("Histogram", ChartSeriesKind.Bar, ChartPane.Oscillator,
          Build(candles, macd.Select(r => D(r.Histogram)).ToArray()), CSlate,
          [new ChartRefLine(0, null, RefSlate)]));
        series.Add(new ChartSeries("MACD", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, macd.Select(r => D(r.Macd)).ToArray()), CBlue, []));
        series.Add(new ChartSeries("Signal", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, macd.Select(r => D(r.Signal)).ToArray()), COrange, []));
        hasOsc = true;
        break;
      }

      case AppIndicator.ADX:
      {
        var t = (AdxIndicatorTemplate)template;
        var adx = candles.ToAdx(t.Period);
        series.Add(new ChartSeries("+DI", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, adx.Select(r => D(r.Pdi)).ToArray()), CGreen, []));
        series.Add(new ChartSeries("-DI", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, adx.Select(r => D(r.Mdi)).ToArray()), DownColor, []));
        series.Add(new ChartSeries("ADX", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, adx.Select(r => D(r.Adx)).ToArray()), CViolet,
          [new ChartRefLine(t.AdxThreshold, "thr", RefSlate)]));
        hasOsc = true; oscMin = 0; oscMax = 100;
        break;
      }

      case AppIndicator.ATR:
      {
        var t = (AtrIndicatorTemplate)template;
        var atr = candles.ToAtr(t.Period);
        series.Add(new ChartSeries("ATR", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, atr.Select(r => D(r.Atr)).ToArray()), COrange, []));
        hasOsc = true;
        break;
      }

      case AppIndicator.OBV:
      {
        var t = (ObvIndicatorTemplate)template;
        var obv = candles.ToObv();
        var obvVals = obv.Select(r => (double?)r.Obv).ToArray();
        var emaVals = Ema(obvVals, t.EmaPeriod);
        series.Add(new ChartSeries("OBV", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, obvVals), CSky, []));
        series.Add(new ChartSeries("OBV EMA", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, emaVals), COrange, []));
        hasOsc = true;
        break;
      }

      case AppIndicator.CCI:
      {
        var t = (CciIndicatorTemplate)template;
        var cci = candles.ToCci(t.Period);
        series.Add(new ChartSeries("CCI", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, cci.Select(r => D(r.Cci)).ToArray()), CSky,
          [new ChartRefLine(t.Overbought, "OB", RefRed), new ChartRefLine(t.Oversold, "OS", RefGreen)]));
        hasOsc = true;
        break;
      }

      case AppIndicator.MFI:
      {
        var t = (MfiIndicatorTemplate)template;
        var mfi = candles.ToMfi(t.Period);
        series.Add(new ChartSeries("MFI", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, mfi.Select(r => D(r.Mfi)).ToArray()), CViolet,
          [new ChartRefLine(t.Overbought, "OB", RefRed), new ChartRefLine(t.Oversold, "OS", RefGreen)]));
        hasOsc = true; oscMin = 0; oscMax = 100;
        break;
      }

      case AppIndicator.WilliamsR:
      {
        var t = (WilliamsRIndicatorTemplate)template;
        var wr = candles.ToWilliamsR(t.Period);
        series.Add(new ChartSeries("Williams %R", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, wr.Select(r => D(r.WilliamsR)).ToArray()), CSky,
          [new ChartRefLine(t.Overbought, "OB", RefRed), new ChartRefLine(t.Oversold, "OS", RefGreen)]));
        hasOsc = true; oscMin = -100; oscMax = 0;
        break;
      }

      case AppIndicator.CMO:
      {
        var t = (CmoIndicatorTemplate)template;
        var cmo = candles.ToCmo(t.Period);
        series.Add(new ChartSeries("CMO", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, cmo.Select(r => D(r.Cmo)).ToArray()), CGreen,
          [new ChartRefLine(t.Overbought, "OB", RefRed), new ChartRefLine(t.Oversold, "OS", RefGreen)]));
        hasOsc = true; oscMin = -100; oscMax = 100;
        break;
      }

      case AppIndicator.TSI:
      {
        var t = (TsiIndicatorTemplate)template;
        var tsi = candles.ToTsi(t.LongPeriods, t.SignalPeriods);
        series.Add(new ChartSeries("TSI", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, tsi.Select(r => D(r.Tsi)).ToArray()), CViolet, [new ChartRefLine(0, null, RefSlate)]));
        series.Add(new ChartSeries("Signal", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, tsi.Select(r => D(r.Signal)).ToArray()), COrange, []));
        hasOsc = true;
        break;
      }

      case AppIndicator.Keltner:
      {
        var t = (KeltnerIndicatorTemplate)template;
        var kc = candles.ToKeltner(t.Period, t.Multiplier, t.AtrPeriod);
        series.Add(new ChartSeries("Keltner Upper", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, kc.Select(r => D(r.UpperBand)).ToArray()), CGreen, []));
        series.Add(new ChartSeries("Keltner Middle", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, kc.Select(r => D(r.Centerline)).ToArray()), RefSlate, []));
        series.Add(new ChartSeries("Keltner Lower", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, kc.Select(r => D(r.LowerBand)).ToArray()), CGreen, []));
        break;
      }

      case AppIndicator.Donchian:
      {
        var t = (DonchianIndicatorTemplate)template;
        var dc = candles.ToDonchian(t.Period);
        series.Add(new ChartSeries("Donchian Upper", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, dc.Select(r => D(r.UpperBand)).ToArray()), CSlate, []));
        series.Add(new ChartSeries("Donchian Middle", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, dc.Select(r => D(r.Centerline)).ToArray()), RefSlate, []));
        series.Add(new ChartSeries("Donchian Lower", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, dc.Select(r => D(r.LowerBand)).ToArray()), CSlate, []));
        break;
      }

      case AppIndicator.WMA:
      {
        var t = (WmaIndicatorTemplate)template;
        var values = candles.Use(t.CandlePart.ToCandlePart()).ToWma(t.Periods).Select(r => D(r.Wma)).ToArray();
        AddAverageLine(series, candles, "WMA", t.Periods, values, t.Deviation, CBlue, BandBlue);
        break;
      }

      case AppIndicator.HMA:
      {
        var t = (HmaIndicatorTemplate)template;
        var values = candles.Use(t.CandlePart.ToCandlePart()).ToHma(t.Periods).Select(r => D(r.Hma)).ToArray();
        AddAverageLine(series, candles, "HMA", t.Periods, values, t.Deviation, CGreen, BandGreen);
        break;
      }

      case AppIndicator.VWMA:
      {
        var t = (VwmaIndicatorTemplate)template;
        var values = candles.ToVwma(t.Periods).Select(r => D(r.Vwma)).ToArray();
        AddAverageLine(series, candles, "VWMA", t.Periods, values, t.Deviation, COrange, BandOrange);
        break;
      }

      case AppIndicator.KAMA:
      {
        var t = (KamaIndicatorTemplate)template;
        var values = candles
          .Use(t.CandlePart.ToCandlePart())
          .ToKama(t.ErPeriods, t.FastPeriods, t.SlowPeriods)
          .Select(r => D(r.Kama)).ToArray();
        AddAverageLine(series, candles, "KAMA", t.ErPeriods, values, t.Deviation, CFuchsia, BandFuchsia);
        break;
      }

      case AppIndicator.PSAR:
      {
        var t = (PsarIndicatorTemplate)template;
        var sar = candles.ToParabolicSar(t.AccelerationStep, t.MaxAccelerationFactor);
        series.Add(new ChartSeries("PSAR", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, sar.Select(r => D(r.Sar)).ToArray()), CSky, []));
        break;
      }

      case AppIndicator.Chandelier:
      {
        var t = (ChandelierIndicatorTemplate)template;
        var type = t.Type == ChandelierDirection.Long ? Direction.Long : Direction.Short;
        var ce = candles.ToChandelier(t.Period, t.Multiplier, type);
        series.Add(new ChartSeries("Chandelier", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, ce.Select(r => D(r.ChandelierExit)).ToArray()), CBlue, []));
        break;
      }

      case AppIndicator.Aroon:
      {
        var t = (AroonIndicatorTemplate)template;
        var aroon = candles.ToAroon(t.Period);
        series.Add(new ChartSeries("Aroon Up", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, aroon.Select(r => D(r.AroonUp)).ToArray()), CGreen, []));
        series.Add(new ChartSeries("Aroon Down", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, aroon.Select(r => D(r.AroonDown)).ToArray()), DownColor, []));
        hasOsc = true; oscMin = 0; oscMax = 100;
        break;
      }

      case AppIndicator.Vortex:
      {
        var t = (VortexIndicatorTemplate)template;
        var vi = candles.ToVortex(t.Period);
        series.Add(new ChartSeries("VI+", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, vi.Select(r => D(r.Pvi)).ToArray()), CGreen, [new ChartRefLine(100, null, RefSlate)]));
        series.Add(new ChartSeries("VI-", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, vi.Select(r => D(r.Nvi)).ToArray()), DownColor, []));
        hasOsc = true;
        break;
      }

      // Point-event indicators: no library series, rely on the trigger marker (+ level line).
      case AppIndicator.Gap:
      case AppIndicator.RoundPrice:
      {
        if (indicator == AppIndicator.RoundPrice)
        {
          var level = (double)candles[^1].Close;
          series.Add(new ChartSeries("Level", ChartSeriesKind.Line, ChartPane.Price, [], RefSlate,
            [new ChartRefLine(level, "round", RefSlate)]));
        }
        break;
      }

      case AppIndicator.Volume:
      {
        var t = (VolumeIndicatorTemplate)template;
        var volumes = candles.Select(candle => (double?)candle.Volume).ToArray();
        series.Add(new ChartSeries("Volume", ChartSeriesKind.Bar, ChartPane.Oscillator,
          Build(candles, volumes), CSlate, []));
        series.Add(new ChartSeries($"Avg {t.Periods}", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, RollingAverage(volumes, t.Periods)), COrange, []));
        hasOsc = true;
        break;
      }

      case AppIndicator.PriceChange:
      {
        var t = (PriceChangeIndicatorTemplate)template;
        var refs = new List<ChartRefLine> { new(0, null, RefSlate) };
        if (t.Direction != PriceChangeDirection.Down)
          refs.Add(new ChartRefLine(t.Percent, "thr", RefRed));
        if (t.Direction != PriceChangeDirection.Up)
          refs.Add(new ChartRefLine(-t.Percent, "thr", RefGreen));
        series.Add(new ChartSeries("Change %", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, ChangePercent(candles, t.Periods)), CBlue, refs));
        hasOsc = true;
        break;
      }

      case AppIndicator.OpenChange:
      {
        var t = (OpenChangeIndicatorTemplate)template;
        var refs = new List<ChartRefLine> { new(0, null, RefSlate) };
        if (t.Direction != PriceChangeDirection.Down)
          refs.Add(new ChartRefLine(t.Percent, "thr", RefRed));
        if (t.Direction != PriceChangeDirection.Up)
          refs.Add(new ChartRefLine(-t.Percent, "thr", RefGreen));
        series.Add(new ChartSeries("Open %", ChartSeriesKind.Line, ChartPane.Oscillator,
          Build(candles, ChangeFromOpen(candles)), CBlue, refs));
        hasOsc = true;
        break;
      }

      case AppIndicator.Week52HighLow:
      {
        var t = (Week52HighLowIndicatorTemplate)template;
        if (t.Extreme is not Week52Extreme.Low)
          series.Add(new ChartSeries($"{t.Weeks}w High", ChartSeriesKind.Line, ChartPane.Price,
            Build(candles, RollingExtremes(candles, t.Weeks, high: true)), CGreen, []));
        if (t.Extreme is not Week52Extreme.High)
          series.Add(new ChartSeries($"{t.Weeks}w Low", ChartSeriesKind.Line, ChartPane.Price,
            Build(candles, RollingExtremes(candles, t.Weeks, high: false)), DownColor, []));
        break;
      }

      case AppIndicator.TrailingStop:
      {
        var t = (TrailingStopIndicatorTemplate)template;
        series.Add(new ChartSeries("Trailing Stop", ChartSeriesKind.Line, ChartPane.Price,
          Build(candles, TrailingStopLine(candles, t)), CBlue, []));
        break;
      }

      default:
        break;
    }

    return new Result(series.ToArray(), hasOsc, oscMin, oscMax);
  }

  /// <summary>
  /// A moving-average overlay line plus its trigger tolerance band, for the MA-family
  /// indicators outside the SMA/EMA pair (WMA, HMA, VWMA, KAMA).
  /// </summary>
  private static void AddAverageLine(
    List<ChartSeries> series, Candle[] candles, string name, int periods, double?[] values,
    double deviation, string color, string bandColor)
  {
    var lineName = $"{name} {periods}";
    series.Add(new ChartSeries(lineName, ChartSeriesKind.Line, ChartPane.Price, Build(candles, values), color, []));
    series.AddRange(DeviationBand(candles, lineName, values, deviation, bandColor, ChartPane.Price, divideUpper: false));
  }

  private static ChartSeries[] BuildMa(Candle[] candles, MaIndicatorTemplate template, string name, string color, string bandColor)
  {
    double?[] values;
    if (template is EmaIndicatorTemplate)
      values = candles.Use(template.CandlePart.ToCandlePart()).ToEma(template.Periods).Select(r => D(r.Ema)).ToArray();
    else
      values = candles.Use(template.CandlePart.ToCandlePart()).ToSma(template.Periods).Select(r => D(r.Sma)).ToArray();

    var lineName = $"{name} {template.Periods}";
    var line = new ChartSeries(lineName, ChartSeriesKind.Line, ChartPane.Price, Build(candles, values), color, []);

    // The trigger zone: a cross fires when the close lands inside line±dev (MovingAverageCommandHandlerBase).
    return [line, ..DeviationBand(candles, lineName, values, template.Deviation, bandColor, ChartPane.Price, divideUpper: false)];
  }

  /// <summary>
  /// Dashed boundary lines of a crossover handler's tolerance band around its signal line:
  /// values inside the band are treated as noise, a signal fires only beyond the boundary.
  /// <paramref name="divideUpper"/> picks the exact threshold the handler compares against:
  /// the stochastic-style handlers test <c>signal &gt; line/(1-dev)</c> upward (so the upper
  /// boundary is line/(1-dev), not line*(1+dev)), the price-style ones <c>price &gt; line*(1+dev)</c>.
  /// Not drawn when the tolerance is 0 (no band) or >= 1 (the handler's math degenerates).
  /// Hidden from legends: they annotate the signal line, not an indicator of their own.
  /// </summary>
  private static ChartSeries[] DeviationBand(Candle[] candles, string name, double?[] values, double deviation, string color, ChartPane pane, bool divideUpper)
  {
    if (deviation <= 0 || deviation >= 1)
      return [];

    return
    [
      Band(name + " +dev", values, divideUpper ? 1.0 / (1.0 - deviation) : 1.0 + deviation),
      Band(name + " -dev", values, 1.0 - deviation),
    ];

    ChartSeries Band(string bandName, double?[] factorValues, double factor) => new(
      bandName, ChartSeriesKind.Line, pane,
      Build(candles, factorValues.Select(v => v.HasValue ? v.Value * factor : (double?)null).ToArray()),
      color, [], Dashed: true, HiddenFromLegend: true);
  }

  private static ChartPoint[] Build(Candle[] candles, double?[] values)
  {
    var points = new List<ChartPoint>(values.Length);
    var n = Math.Min(candles.Length, values.Length);

    for (var i = 0; i < n; i++)
      if (values[i] is { } value)
        points.Add(new ChartPoint(ChartX.ToX(candles[i].Timestamp), value));

    return points.ToArray();
  }

  /// <summary>
  /// Average of the values on the <paramref name="periods"/> candles preceding each index
  /// (the value's own candle excluded) - the same window the Volume handler compares against.
  /// </summary>
  private static double?[] RollingAverage(double?[] values, int periods)
  {
    var result = new double?[values.Length];

    for (var i = periods; i < values.Length; i++)
    {
      double sum = 0;

      for (var j = i - periods; j < i; j++)
        sum += values[j] ?? 0;

      result[i] = sum / periods;
    }

    return result;
  }

  /// <summary>
  /// Percent change of each candle's close against the close the given number of candles back -
  /// the same math the PriceChange handler triggers on.
  /// </summary>
  private static double?[] ChangePercent(Candle[] candles, int periods)
  {
    var result = new double?[candles.Length];

    for (var i = periods; i < candles.Length; i++)
    {
      var baseClose = candles[i - periods].Close;
      result[i] = baseClose == 0m ? null : (double)(candles[i].Close / baseClose - 1m) * 100.0;
    }

    return result;
  }

  /// <summary>
  /// Percent change of each candle's close from its own open - the same math the OpenChange
  /// handler triggers on.
  /// </summary>
  private static double?[] ChangeFromOpen(Candle[] candles)
  {
    var result = new double?[candles.Length];

    for (var i = 0; i < candles.Length; i++)
      result[i] = candles[i].Open == 0m ? null : (double)(candles[i].Close / candles[i].Open - 1m) * 100.0;

    return result;
  }

  /// <summary>
  /// Past-only rolling extreme over the calendar window of the given weeks ending right before
  /// each candle (the candle's own bar excluded, exactly as the Week52 handler measures the
  /// previous extremes against the close). The line stays null until the window actually covers
  /// ~90% of the calendar span - the same coverage guard the handler applies, so a young chart
  /// neither shows nor triggers on a bogus "extreme" of a handful of candles.
  /// </summary>
  private static double?[] RollingExtremes(Candle[] candles, int weeks, bool high)
  {
    const double coverageRatio = 0.9;

    var result = new double?[candles.Length];
    var span = TimeSpan.FromDays(weeks * 7L);
    var coverageFrom = TimeSpan.FromDays(weeks * 7L * coverageRatio);

    for (var i = 1; i < candles.Length; i++)
    {
      var windowFrom = candles[i].Timestamp - span;
      double? extreme = null;

      var j = i - 1;
      for (; j >= 0 && candles[j].Timestamp >= windowFrom; j--)
      {
        var value = high ? (double)candles[j].High : (double)candles[j].Low;
        extreme = extreme is null ? value : high ? Math.Max(extreme.Value, value) : Math.Min(extreme.Value, value);
      }

      // candles[j + 1] is the oldest window member (the reverse walk stopped at the first bar
      // older than the window); the handler only trusts extremes once it reaches into the
      // window's last 10%, and skips the evaluation entirely before that.
      if (extreme is null || j + 1 >= i || candles[j + 1].Timestamp > candles[i].Timestamp - coverageFrom)
        continue;

      result[i] = extreme;
    }

    return result;
  }

  /// <summary>
  /// Replay of the TrailingStop handler's ratchet: a long anchors on the running max of highs
  /// (stop under it), a short on the running min of lows (stop above it); the line is defined
  /// from the very first candle, just as the handler arms its anchor on the first check.
  /// </summary>
  private static double?[] TrailingStopLine(Candle[] candles, TrailingStopIndicatorTemplate template)
  {
    var result = new double?[candles.Length];
    var isLong = template.Type == ChandelierDirection.Long;
    double? anchor = null;

    for (var i = 0; i < candles.Length; i++)
    {
      var extreme = isLong ? (double)candles[i].High : (double)candles[i].Low;
      anchor = anchor is null ? extreme : isLong ? Math.Max(anchor.Value, extreme) : Math.Min(anchor.Value, extreme);

      result[i] = template.ThresholdType == TrailingThresholdType.Percent
        ? isLong ? anchor.Value * (1 - template.Value / 100) : anchor.Value * (1 + template.Value / 100)
        : isLong ? anchor.Value - template.Value : anchor.Value + template.Value;
    }

    return result;
  }

  private static double?[] Ema(double?[] values, int period)
  {
    var result = new double?[values.Length];

    if (values.Length < period || period < 1)
      return result;

    var k = 2.0 / (period + 1);
    var seed = 0.0;

    for (var i = 0; i < period; i++)
      seed += values[i] ?? 0;

    var ema = seed / period;

    // Same recurrence as ObvIndicatorCommandHandler.ComputeEma: SMA seed over the first
    // `period` values, then the standard update; undefined (null) before the seed candle.
    for (var i = period - 1; i < values.Length; i++)
    {
      if (i > period - 1)
        ema = ema + k * ((values[i] ?? 0) - ema);

      result[i] = values[i] is null ? null : ema;
    }

    return result;
  }

  private static double? D(double? value) => value;

  private static double? D(decimal? value) => value.HasValue ? (double?)value.Value : null;
}
