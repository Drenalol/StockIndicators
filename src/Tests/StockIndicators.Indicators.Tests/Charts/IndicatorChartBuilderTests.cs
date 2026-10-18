using FacioQuo.Stock.Indicators;
using StockIndicators.Application.Charts;
using StockIndicators.Domain.Models.Candles;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class IndicatorChartBuilderTests
{
  private const int CandleCount = 80;

  private static Candle[] CreateCandles()
  {
    var candles = new Candle[CandleCount];
    var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    for (var i = 0; i < CandleCount; i++)
    {
      var open = 100m + i * 0.1m;
      var close = 100m + (i + 1) * 0.1m - (i % 5 == 0 ? 0.3m : 0m);

      candles[i] = new Candle(
        "TEST",
        startDate.AddDays(i),
        TimeFrame.Day,
        open,
        Math.Max(open, close) + 0.2m,
        Math.Min(open, close) - 0.2m,
        close,
        1000
      );
    }

    return candles;
  }

  [Fact]
  public void Build_Sma_DrawsPriceLineWithExpectedPointsPlusHiddenDeviationBand()
  {
    var candles = CreateCandles();
    var template = new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 };

    var result = IndicatorChartBuilder.Build(AppIndicator.SMA, template, candles);

    var series = result.Series.Single(s => !s.HiddenFromLegend);

    Assert.Equal(3, result.Series.Count);
    Assert.Equal($"SMA {template.Periods}", series.Name);
    Assert.False(series.Dashed);
    Assert.Equal(ChartPane.Price, series.Pane);
    Assert.Equal(ChartSeriesKind.Line, series.Kind);
    Assert.Equal(candles.Length - template.Periods + 1, series.Points.Count);
    Assert.False(result.HasOscillator);

    var band = result.Series.Where(s => s.HiddenFromLegend).ToArray();
    Assert.Equal([$"SMA {template.Periods} +dev", $"SMA {template.Periods} -dev"], band.Select(s => s.Name).ToArray());
    Assert.All(band, s =>
    {
      Assert.True(s.Dashed);
      Assert.Equal(ChartPane.Price, s.Pane);
      Assert.Equal(series.Points.Count, s.Points.Count);
    });

    // The band matches the handler's trigger zone: line*(1±dev).
    AssertBandScaledBy(series.Points, band[0].Points, 1 + template.Deviation);
    AssertBandScaledBy(series.Points, band[1].Points, 1 - template.Deviation);
  }

  [Fact]
  public void Build_Ema_DrawsPriceLineAndBand()
  {
    var candles = CreateCandles();
    var template = new EmaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 };

    var result = IndicatorChartBuilder.Build(AppIndicator.EMA, template, candles);

    var series = result.Series.Single(s => !s.HiddenFromLegend);

    Assert.Equal(3, result.Series.Count);
    Assert.Equal($"EMA {template.Periods}", series.Name);
    Assert.Equal(ChartPane.Price, series.Pane);
    Assert.True(series.Points.Count > 0);
    Assert.False(result.HasOscillator);
  }

  [Fact]
  public void Build_MaZeroDeviation_DrawsNoBand()
  {
    var candles = CreateCandles();
    var template = new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10, Deviation = 0 };

    var result = IndicatorChartBuilder.Build(AppIndicator.SMA, template, candles);

    var series = Assert.Single(result.Series);
    Assert.False(series.HiddenFromLegend);
  }

  [Theory]
  [InlineData(AppIndicator.StochRSI)]
  [InlineData(AppIndicator.Stochastic)]
  public void Build_StochasticStyle_DrawsHiddenBandAtHandlerThresholds(AppIndicator indicator)
  {
    var candles = CreateCandles();
    IIndicatorTemplate template = indicator == AppIndicator.StochRSI
      ? new StochRsiIndicatorTemplate { TimeFrame = TimeFrame.Day, RsiPeriod = 14, StochPeriod = 14, SignalPeriod = 3, SmoothPeriod = 3 }
      : new StochasticIndicatorTemplate { TimeFrame = TimeFrame.Day, Period = 14, SignalPeriod = 3, SmoothPeriod = 3 };

    var result = IndicatorChartBuilder.Build(indicator, template, candles);

    var percD = result.Series.Single(s => s.Name == "%D");
    var band = result.Series.Where(s => s.HiddenFromLegend).ToArray();

    Assert.Equal(4, result.Series.Count);
    Assert.Equal(["%D +dev", "%D -dev"], band.Select(s => s.Name).ToArray());
    Assert.All(band, s =>
    {
      Assert.True(s.Dashed);
      Assert.Equal(ChartPane.Oscillator, s.Pane);
      Assert.Equal(percD.Points.Count, s.Points.Count);
    });

    // The band is the exact crossover threshold of the handlers: an up-signal needs
    // %K > %D/(1-dev), a down-signal %K < %D*(1-dev).
    var dev = indicator == AppIndicator.StochRSI
      ? ((StochRsiIndicatorTemplate)template).Deviation
      : ((StochasticIndicatorTemplate)template).Deviation;
    AssertBandScaledBy(percD.Points, band[0].Points, 1.0 / (1.0 - dev));
    AssertBandScaledBy(percD.Points, band[1].Points, 1.0 - dev);
  }

  private static void AssertBandScaledBy(IReadOnlyList<ChartPoint> line, IReadOnlyList<ChartPoint> band, double factor)
  {
    Assert.Equal(line.Count, band.Count);

    for (var i = 0; i < line.Count; i++)
    {
      Assert.Equal(line[i].X, band[i].X);
      Assert.Equal(line[i].Value * factor, band[i].Value, 1e-9);
    }
  }

  [Fact]
  public void Build_Rsi_DrawsOscillatorLineWithReferenceLines()
  {
    var candles = CreateCandles();
    var template = new RsiIndicatorTemplate { TimeFrame = TimeFrame.Day, Period = 14, Overbought = 70, Oversold = 30 };

    var result = IndicatorChartBuilder.Build(AppIndicator.RSI, template, candles);

    var series = Assert.Single(result.Series);

    Assert.Equal(ChartPane.Oscillator, series.Pane);
    Assert.True(result.HasOscillator);
    Assert.Equal(0, result.OscMin);
    Assert.Equal(100, result.OscMax);

    var expectedPoints = candles.ToRsi(template.Period).Count(r => r.Rsi.HasValue);
    Assert.Equal(expectedPoints, series.Points.Count);

    var referenceYs = series.RefLines.Select(r => r.Y).OrderBy(y => y).ToArray();
    Assert.Equal([30, 70], referenceYs);
  }

  [Fact]
  public void Build_Macd_DrawsHistogramAndTwoLinesOnOscillatorPane()
  {
    var candles = CreateCandles();
    var template = new MacdIndicatorTemplate { TimeFrame = TimeFrame.Day, FastPeriods = 12, SlowPeriods = 26, SignalPeriods = 9 };

    var result = IndicatorChartBuilder.Build(AppIndicator.MACD, template, candles);

    Assert.Equal(3, result.Series.Count);
    Assert.True(result.HasOscillator);

    var histogram = result.Series.Single(s => s.Name == "Histogram");
    var macd = result.Series.Single(s => s.Name == "MACD");
    var signal = result.Series.Single(s => s.Name == "Signal");

    Assert.Equal(ChartSeriesKind.Bar, histogram.Kind);
    Assert.Equal(ChartSeriesKind.Line, macd.Kind);
    Assert.All(new[] { histogram, macd, signal }, s => Assert.Equal(ChartPane.Oscillator, s.Pane));

    Assert.Contains(0, histogram.RefLines.Select(r => r.Y));
    Assert.True(macd.Points.Count >= signal.Points.Count);
    Assert.True(signal.Points.Count >= histogram.Points.Count);
  }

  [Fact]
  public void Build_Ichimoku_DrawsFivePriceSeriesWithoutOscillator()
  {
    var candles = CreateCandles();
    var template = new IchimokuIndicatorTemplate { TimeFrame = TimeFrame.Day, TenkanPeriod = 9, KijunPeriod = 26, SenkouBPeriod = 52 };

    var result = IndicatorChartBuilder.Build(AppIndicator.Ichimoku, template, candles);

    var names = result.Series.Select(s => s.Name).OrderBy(n => n).ToArray();
    Assert.Equal(["Chikou", "Kijun", "Span A", "Span B", "Tenkan"], names);
    Assert.All(result.Series, s => Assert.Equal(ChartPane.Price, s.Pane));
    Assert.False(result.HasOscillator);
  }

  [Fact]
  public void Build_Ichimoku_DrawsLeadingSpansOffsetForwardByKijunPeriod()
  {
    var candles = CreateCandles();
    var template = new IchimokuIndicatorTemplate { TimeFrame = TimeFrame.Day, TenkanPeriod = 9, KijunPeriod = 26, SenkouBPeriod = 52 };

    var result = IndicatorChartBuilder.Build(AppIndicator.Ichimoku, template, candles);

    var spanA = result.Series.Single(s => s.Name == "Span A");
    var spanB = result.Series.Single(s => s.Name == "Span B");
    var chikou = result.Series.Single(s => s.Name == "Chikou");

    // The library returns every Ichimoku line already display-offset (spanA[i] is the cloud
    // slice computed KijunPeriod bars earlier, Chikou[i] the close Kijun bars later): the
    // builder must draw rows on their own dates, any extra shift would double-offset the
    // picture. Span A's first row lands after SenkouB's warmup + the kijun displacement,
    // Span B's one KijunPeriod later still, and Chikou starts on the very first candle.
    Assert.Equal(ChartX.ToX(candles[template.SenkouBPeriod - 1].Timestamp), spanA.Points[0].X, 1e-9);
    Assert.Equal(ChartX.ToX(candles[template.SenkouBPeriod - 1 + template.KijunPeriod].Timestamp), spanB.Points[0].X, 1e-9);
    Assert.Equal(ChartX.ToX(candles[^1].Timestamp), spanA.Points[^1].X, 1e-9);
    Assert.Equal(ChartX.ToX(candles[0].Timestamp), chikou.Points[0].X, 1e-9);
  }

  [Fact]
  public void Build_Obv_DrawsObvLineAndEmaMatchingHandlerRecurrence()
  {
    var candles = CreateCandles();
    var template = new ObvIndicatorTemplate { TimeFrame = TimeFrame.Day, EmaPeriod = 20 };

    var result = IndicatorChartBuilder.Build(AppIndicator.OBV, template, candles);

    var obv = result.Series.Single(s => s.Name == "OBV");
    var ema = result.Series.Single(s => s.Name == "OBV EMA");

    Assert.Equal(2, result.Series.Count);
    Assert.True(result.HasOscillator);
    Assert.All(new[] { obv, ema }, s =>
    {
      Assert.Equal(ChartPane.Oscillator, s.Pane);
      Assert.Equal(ChartSeriesKind.Line, s.Kind);
    });

    // Replay the handler's own math: ObvIndicatorCommandHandler.AccumulateObv + ComputeEma.
    var obvValues = new double[candles.Length];
    double running = 0;

    for (var i = 1; i < candles.Length; i++)
    {
      if (candles[i].Close > candles[i - 1].Close)
        running += (double)candles[i].Volume;
      else if (candles[i].Close < candles[i - 1].Close)
        running -= (double)candles[i].Volume;

      obvValues[i] = running;
    }

    Assert.Equal(candles.Length, obv.Points.Count);
    Assert.All(obv.Points.Select((p, i) => (p.Value, Expected: obvValues[i])), t => Assert.Equal(t.Expected, t.Value, 1e-9));

    var k = 2.0 / (template.EmaPeriod + 1);

    // The EMA series starts at its seed candle and equals the handler's recurrence point by point.
    Assert.Equal(candles.Length - template.EmaPeriod + 1, ema.Points.Count);

    for (var i = 0; i < ema.Points.Count; i++)
      Assert.Equal(ExpectedEmaAt(template.EmaPeriod - 1 + i), ema.Points[i].Value, 1e-9);

    double ExpectedEmaAt(int index)
    {
      var emaValue = obvValues.Take(template.EmaPeriod).Average();

      for (var i = template.EmaPeriod; i <= index; i++)
        emaValue = emaValue + k * (obvValues[i] - emaValue);

      return emaValue;
    }

    // Regression for the transposed-update bug (v + k*(v-ema) instead of ema + k*(v-ema)):
    // a real EMA lags the OBV line, it never rides on top of it.
    var lastGap = Math.Abs(obv.Points[^1].Value - ema.Points[^1].Value);
    var obvRange = obv.Points.Max(p => p.Value) - obv.Points.Min(p => p.Value);
    Assert.True(lastGap > obvRange * 0.005,
      $"OBV EMA must lag OBV by a visible margin, got {lastGap} for range {obvRange}");
  }

  [Fact]
  public void Build_Gap_DrawsNoSeries()
  {
    var candles = CreateCandles();
    var template = new GapIndicatorTemplate { TimeFrame = TimeFrame.Day, Percent = 5 };

    var result = IndicatorChartBuilder.Build(AppIndicator.Gap, template, candles);

    Assert.Empty(result.Series);
    Assert.False(result.HasOscillator);
  }

  [Fact]
  public void Build_RoundPrice_DrawsLevelLineAtLastClose()
  {
    var candles = CreateCandles();
    var template = new RoundPriceIndicatorTemplate { Accuracy = 500 };

    var result = IndicatorChartBuilder.Build(AppIndicator.RoundPrice, template, candles);

    var series = Assert.Single(result.Series);
    Assert.Equal(ChartPane.Price, series.Pane);
    Assert.Equal((double)candles[^1].Close, series.RefLines.Single().Y);
  }

  [Fact]
  public void Build_Volume_DrawsVolumeBarsAndAverageLineOnOscillator()
  {
    var candles = CreateCandles();
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10, Multiplier = 2 };

    var result = IndicatorChartBuilder.Build(AppIndicator.Volume, template, candles);

    Assert.True(result.HasOscillator);

    var volume = result.Series.Single(s => s.Name == "Volume");
    var average = result.Series.Single(s => s.Name == "Avg 10");

    Assert.Equal(ChartSeriesKind.Bar, volume.Kind);
    Assert.Equal(ChartSeriesKind.Line, average.Kind);
    Assert.All(new[] { volume, average }, s => Assert.Equal(ChartPane.Oscillator, s.Pane));

    // Every candle has volume; the average excludes the window's leading candles
    Assert.Equal(candles.Length, volume.Points.Count);
    Assert.Equal(candles.Length - template.Periods, average.Points.Count);
  }

  [Theory]
  [InlineData(PriceChangeDirection.Up, new double[] { 0, 1 })]
  [InlineData(PriceChangeDirection.Down, new double[] { -1, 0 })]
  [InlineData(PriceChangeDirection.Both, new double[] { -1, 0, 1 })]
  public void Build_PriceChange_DrawsChangeLineWithDirectionalThresholds(PriceChangeDirection direction, double[] expectedRefLines)
  {
    var candles = CreateCandles();
    var template = new PriceChangeIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10, Percent = 1, Direction = direction };

    var result = IndicatorChartBuilder.Build(AppIndicator.PriceChange, template, candles);

    Assert.True(result.HasOscillator);

    var series = Assert.Single(result.Series);

    Assert.Equal(ChartPane.Oscillator, series.Pane);
    Assert.Equal(candles.Length - template.Periods, series.Points.Count);
    Assert.Equal(expectedRefLines, series.RefLines.Select(r => r.Y).OrderBy(y => y).ToArray());
  }

}
