using StockIndicators.Application.Charts;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class IndicatorChartBuilderMergeTests
{
  private static ChartSeries Series(string name, ChartPane pane, string color) =>
    new(name, ChartSeriesKind.Line, pane, [], color, []);

  [Fact]
  public void Merge_KeepsTheSharedOscillatorRange_WhenAllOscillatorsShareIt()
  {
    var rsi = new IndicatorChartBuilder.Result([Series("RSI", ChartPane.Oscillator, "#8b5cf6")], true, 0, 100);
    var stoch = new IndicatorChartBuilder.Result(
      [Series("%K", ChartPane.Oscillator, "#0ea5e9"), Series("%D", ChartPane.Oscillator, "#f97316")],
      true,
      0,
      100);

    var merged = IndicatorChartBuilder.Merge([rsi, stoch]);

    Assert.True(merged.HasOscillator);
    Assert.Equal(0, merged.OscMin);
    Assert.Equal(100, merged.OscMax);
    Assert.Equal(3, merged.Series.Count);
  }

  [Fact]
  public void Merge_Autoscales_WhenARangedOscillatorMeetsAnUnboundedOne()
  {
    var rsi = new IndicatorChartBuilder.Result([Series("RSI", ChartPane.Oscillator, "#8b5cf6")], true, 0, 100);
    var macd = new IndicatorChartBuilder.Result(
      [Series("MACD", ChartPane.Oscillator, "#2563eb"), Series("Histogram", ChartPane.Oscillator, "#94a3b8")],
      true,
      null,
      null);

    var merged = IndicatorChartBuilder.Merge([rsi, macd]);

    Assert.True(merged.HasOscillator);
    Assert.Null(merged.OscMin);
    Assert.Null(merged.OscMax);
    Assert.Equal(3, merged.Series.Count);
  }

  [Fact]
  public void Merge_ShiftsAClashedColor_AlongThePalette()
  {
    var ema21 = new IndicatorChartBuilder.Result([Series("EMA 21", ChartPane.Price, "#0ea5e9")], false, null, null);
    var ema50 = new IndicatorChartBuilder.Result([Series("EMA 50", ChartPane.Price, "#0ea5e9")], false, null, null);

    var merged = IndicatorChartBuilder.Merge([ema21, ema50]);

    Assert.Equal("#0ea5e9", merged.Series[0].Color);
    Assert.NotEqual(merged.Series[0].Color, merged.Series[1].Color);
  }

  [Fact]
  public void Merge_KeepsItsOwnColors_ForALoneResult()
  {
    // Bollinger draws its two bands in one hue on purpose: a single-result merge must not shift them
    var bollinger = new IndicatorChartBuilder.Result(
      [Series("BB Upper", ChartPane.Price, "#8b5cf6"), Series("BB Lower", ChartPane.Price, "#8b5cf6")],
      false,
      null,
      null);

    var merged = IndicatorChartBuilder.Merge([bollinger]);

    Assert.Equal("#8b5cf6", merged.Series[0].Color);
    Assert.Equal("#8b5cf6", merged.Series[1].Color);
  }

  [Fact]
  public void Merge_KeepsIntraIndicatorHues_WhenAnotherIndicatorJoins()
  {
    // A hue an indicator reuses within itself (the Bollinger bands) must survive a merge
    // with another indicator; the colliding other-indicator series is the one that shifts
    var bollinger = new IndicatorChartBuilder.Result(
      [Series("BB Upper", ChartPane.Price, "#8b5cf6"), Series("BB Lower", ChartPane.Price, "#8b5cf6")],
      false,
      null,
      null);
    var rsi = new IndicatorChartBuilder.Result([Series("RSI", ChartPane.Oscillator, "#8b5cf6")], true, 0, 100);

    var merged = IndicatorChartBuilder.Merge([bollinger, rsi]);

    Assert.Equal("#8b5cf6", merged.Series[0].Color);
    Assert.Equal("#8b5cf6", merged.Series[1].Color);
    Assert.NotEqual("#8b5cf6", merged.Series[2].Color);
  }

  [Fact]
  public void Merge_NoOscillator_WhenNoneOfTheIndicatorsUsesOne()
  {
    var sma = new IndicatorChartBuilder.Result([Series("SMA 20", ChartPane.Price, "#2563eb")], false, null, null);
    var vwap = new IndicatorChartBuilder.Result([Series("VWAP", ChartPane.Price, "#d946ef")], false, null, null);

    var merged = IndicatorChartBuilder.Merge([sma, vwap]);

    Assert.False(merged.HasOscillator);
    Assert.Null(merged.OscMin);
    Assert.Null(merged.OscMax);
    Assert.Equal(2, merged.Series.Count);
  }
}
