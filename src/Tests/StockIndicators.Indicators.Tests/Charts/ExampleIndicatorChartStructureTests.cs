using StockIndicators.Application.Charts;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class ExampleIndicatorChartStructureTests
{
  [Theory]
  [InlineData(Indicator.SMA)]
  [InlineData(Indicator.EMA)]
  [InlineData(Indicator.StochRSI)]
  [InlineData(Indicator.RoundPrice)]
  [InlineData(Indicator.Gap)]
  [InlineData(Indicator.MACD)]
  [InlineData(Indicator.RSI)]
  [InlineData(Indicator.BollingerBands)]
  [InlineData(Indicator.SuperTrend)]
  [InlineData(Indicator.VWAP)]
  [InlineData(Indicator.ADX)]
  [InlineData(Indicator.Stochastic)]
  [InlineData(Indicator.ATR)]
  [InlineData(Indicator.Ichimoku)]
  [InlineData(Indicator.OBV)]
  [InlineData(Indicator.CCI)]
  [InlineData(Indicator.MFI)]
  [InlineData(Indicator.WilliamsR)]
  [InlineData(Indicator.CMO)]
  [InlineData(Indicator.TSI)]
  [InlineData(Indicator.Keltner)]
  [InlineData(Indicator.Donchian)]
  [InlineData(Indicator.WMA)]
  [InlineData(Indicator.HMA)]
  [InlineData(Indicator.VWMA)]
  [InlineData(Indicator.KAMA)]
  [InlineData(Indicator.PSAR)]
  [InlineData(Indicator.Chandelier)]
  [InlineData(Indicator.Aroon)]
  [InlineData(Indicator.Vortex)]
  [InlineData(Indicator.OpenChange)]
  [InlineData(Indicator.Week52HighLow)]
  [InlineData(Indicator.TrailingStop)]
  public void ExampleChart_HasVisibleIndicatorContent(Indicator indicator)
  {
    var template = (IIndicatorTemplate)Activator.CreateInstance(indicator.GetTemplate())!;
    ExampleIndicatorChart.ApplySampleDefaults(template);

    var timeFrame = template is ITimeFrameDependentIndicatorTemplate timeFrameDependent
      ? timeFrameDependent.TimeFrame
      : TimeFrame.Day;

    var candles = ExampleCandles.Generate(indicator, timeFrame, ExampleIndicatorChart.ResolveCandleCount(template));
    var built = IndicatorChartBuilder.Build(indicator, template, candles);
    var visible = TriggerChartService.TrimToIndicatorRange(candles, built.Series);

    // The chart must still span a meaningful range after the warmup trim.
    Assert.True(visible.Length >= 50, $"{indicator}: only {visible.Length} candles left after trim");

    if (indicator == Indicator.Gap)
    {
      // Gap has no overlay series by design — the candles themselves are the example.
      Assert.Empty(built.Series);
      return;
    }

    // Every other indicator must draw either line/bar points (enough to be visible)
    // or at least a reference line (RoundPrice draws only a level line).
    Assert.True(
      built.Series.Any(s => s.Points.Count >= 20 || s.RefLines.Count > 0),
      $"{indicator}: no visible indicator series on the example chart"
    );
  }
}
