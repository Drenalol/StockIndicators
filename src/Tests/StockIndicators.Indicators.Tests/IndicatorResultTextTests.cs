using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Indicators.PriceChange;
using StockIndicators.Application.Indicators.StochRsi;
using StockIndicators.Application.Indicators.Volume;
using StockIndicators.Domain.Models.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class IndicatorResultTextTests
{
  private static readonly UserLocale MoscowRu = new(UserLocale.Russian, TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));
  private static readonly UserLocale UtcEn = new(UserLocale.English, TimeZoneInfo.Utc);

  [Fact]
  public void Gap_PreviousDateIsDrawnInUserTimezone()
  {
    // 23:00 UTC on 14 Jan is 02:00 on 15 Jan in Moscow: the "prev" date rolls over
    var result = new GapIndicatorResult
    {
      Percent = 1.5,
      Previous = new DateTime(2024, 1, 14, 23, 0, 0, DateTimeKind.Utc)
    };

    Assert.Equal("1.50% (пред: 2024-01-15)", IndicatorResultText.Build(result, MoscowRu));
    Assert.Equal("1.50% (prev: 2024-01-14)", IndicatorResultText.Build(result, UtcEn));
  }

  [Theory]
  [InlineData(MaTest.Upward, UserLocale.Russian, "100.0000 (вверх)")]
  [InlineData(MaTest.Upward, UserLocale.English, "100.0000 (upward)")]
  [InlineData(MaTest.Downward, UserLocale.Russian, "100.0000 (вниз)")]
  [InlineData(MaTest.Downward, UserLocale.English, "100.0000 (downward)")]
  [InlineData(MaTest.None, UserLocale.Russian, "100.0000 (без пересечения)")]
  [InlineData(MaTest.None, UserLocale.English, "100.0000 (none)")]
  public void Ma_TestLabelIsLocalized(MaTest test, string language, string expected)
  {
    var result = new MaIndicatorResult { Result = 100, Test = test };
    var locale = new UserLocale(language, TimeZoneInfo.Utc);

    Assert.Equal(expected, IndicatorResultText.Build(result, locale));
  }

  [Theory]
  [InlineData(UserLocale.Russian, "Сработал")]
  [InlineData(UserLocale.English, "Triggered")]
  public void DefaultText_IsLocalized(string language, string expected)
  {
    var locale = new UserLocale(language, TimeZoneInfo.Utc);

    // A plain IndicatorResult falls into the default (no dedicated arm) case
    Assert.Equal(expected, IndicatorResultText.Build(new IndicatorResult(), locale));
  }

  [Fact]
  public void StochRsi_TextIsInvariantAcrossLanguages()
  {
    var result = new StochRsiIndicatorResult
    {
      Arrow = StochRsiIndicatorResult.ArrowUp,
      PercK = 70.5,
      PercD = 60.25,
      Delta = 0.1234
    };

    Assert.Equal(IndicatorResultText.Build(result, UtcEn), IndicatorResultText.Build(result, MoscowRu));
  }

  [Theory]
  [InlineData(UserLocale.English, "2.50× avg")]
  [InlineData(UserLocale.Russian, "2.50× среднего")]
  [InlineData(UserLocale.Spanish, "2.50× media")]
  public void Volume_RatioIsFormattedWithLocalizedLabel(string language, string expected)
  {
    var result = new VolumeIndicatorResult { Volume = 2500m, Average = 1000m, Ratio = 2.5 };
    var locale = new UserLocale(language, TimeZoneInfo.Utc);

    Assert.Equal(expected, IndicatorResultText.Build(result, locale));
  }

  [Theory]
  [InlineData(5.0, "+5.00%")]
  [InlineData(-5.0, "-5.00%")]
  [InlineData(0.0, "0.00%")]
  public void PriceChange_SignIsAlwaysVisible(double change, string expected)
  {
    var result = new PriceChangeIndicatorResult { Change = change };

    Assert.Equal(expected, IndicatorResultText.Build(result, UtcEn));
    Assert.Equal(expected, IndicatorResultText.Build(result, MoscowRu));
  }
}
