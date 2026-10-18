using StockIndicators.Application.Charts;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using Xunit;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;

namespace StockIndicators.Indicators.Tests.Charts;

public class ExampleIndicatorChartTests
{
  private static IIndicatorTemplate Create(Indicator indicator)
  {
    var template = (IIndicatorTemplate)Activator.CreateInstance(indicator.GetTemplate())!;
    ExampleIndicatorChart.ApplySampleDefaults(template);

    return template;
  }

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
  [InlineData(Indicator.OpenChange)]
  [InlineData(Indicator.Week52HighLow)]
  [InlineData(Indicator.TrailingStop)]
  public void RenderChartData_HasCandlesAndTrigger(Indicator indicator)
  {
    var data = ExampleIndicatorChart.RenderChartData(indicator, Create(indicator));

    // the fixture window survives the warmup trim with a readable chart left
    Assert.True(data.Candles.Count >= 50, $"{indicator} example chart has only {data.Candles.Count} candles");
    Assert.NotNull(data.Trigger);
    Assert.All(data.Candles, candle => Assert.True(candle.Time > 0));
  }

  [Fact]
  public void RenderChartData_IsDeterministic()
  {
    // records compare arrays by reference, so determinism is verified on the wire form
    var first = System.Text.Json.JsonSerializer.Serialize(ExampleIndicatorChart.RenderChartData(Indicator.RSI, Create(Indicator.RSI)));
    var second = System.Text.Json.JsonSerializer.Serialize(ExampleIndicatorChart.RenderChartData(Indicator.RSI, Create(Indicator.RSI)));

    Assert.Equal(first, second);
  }

  [Theory]
  [InlineData("en", "AAPL RSI Day (Example)")]
  [InlineData("ru", "AAPL RSI Day (Пример)")]
  [InlineData("es", "AAPL RSI Day (Ejemplo)")]
  public void BuildTitle_ShowsTickerTimeframeAndLocalizedSuffix(string lang, string expected) =>
    Assert.Equal(expected, ExampleIndicatorChart.BuildTitle(Indicator.RSI, "AAPL", TimeFrame.Day, lang));

  [Fact]
  public void BuildTitle_WithoutTicker_OmitsIt() =>
    Assert.Equal("RSI Day (Example)", ExampleIndicatorChart.BuildTitle(Indicator.RSI, null, TimeFrame.Day, "en"));

  [Fact]
  public void ResolveCandleCount_CoversTemplateRequirement()
  {
    // VWAP's default window is 390 candles: fewer than that and every value is null.
    var vwap = new VwapIndicatorTemplate { TimeFrame = TimeFrame.Minute };

    Assert.True(ExampleIndicatorChart.ResolveCandleCount(vwap) >= vwap.GetCandleCount());

    var sma = new MaIndicatorTemplate { Periods = 200 };

    Assert.True(ExampleIndicatorChart.ResolveCandleCount(sma) >= 200);
  }

  [Fact]
  public void Generate_Gap_ContainsVisibleGap()
  {
    var candles = ExampleCandles.Generate(AppIndicator.Gap, TimeFrame.Day, 120);
    var index = candles.Length * 2 / 3;

    var gapPercent = (candles[index].Open - candles[index - 1].Close) / candles[index - 1].Close * 100m;

    Assert.InRange(gapPercent, 2.5m, 3.5m);
  }

  [Fact]
  public void Generate_IsDeterministic()
  {
    var first = ExampleCandles.Generate(AppIndicator.SMA, TimeFrame.Day, 120);
    var second = ExampleCandles.Generate(AppIndicator.SMA, TimeFrame.Day, 120);

    Assert.Equal(first.Select(c => c.Close).ToArray(), second.Select(c => c.Close).ToArray());
    Assert.Equal(first.Select(c => c.Volume).ToArray(), second.Select(c => c.Volume).ToArray());
  }

  [Fact]
  public void Generate_AlignsDatesToPeriod()
  {
    var candles = ExampleCandles.Generate(AppIndicator.RSI, TimeFrame.Day, 10);

    foreach (var candle in candles)
      Assert.Equal(TimeSpan.Zero, candle.Timestamp.TimeOfDay);

    var hourly = ExampleCandles.Generate(AppIndicator.RSI, TimeFrame.Hour, 10);

    foreach (var candle in hourly)
    {
      Assert.Equal(0, candle.Timestamp.Minute);
      Assert.Equal(0, candle.Timestamp.Second);
    }
  }

  [Fact]
  public void ApplySampleDefaults_FillsZeroDefaults()
  {
    var sma = new MaIndicatorTemplate();

    ExampleIndicatorChart.ApplySampleDefaults(sma);

    Assert.Equal(20, sma.Periods);

    var stochRsi = new StochRsiIndicatorTemplate();

    ExampleIndicatorChart.ApplySampleDefaults(stochRsi);

    Assert.Equal(14, stochRsi.RsiPeriod);
    Assert.Equal(14, stochRsi.StochPeriod);
    Assert.Equal(3, stochRsi.SignalPeriod);
    Assert.Equal(3, stochRsi.SmoothPeriod);
  }
}
