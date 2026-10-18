using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.BusinessQuant.Mappers;
using StockIndicators.Providers.BusinessQuant.Schema;
using Xunit;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Tests;

public class BusinessQuantMapperTests
{
  [Theory]
  [InlineData("NASDAQ", "NASDAQ")]
  [InlineData("nasdaq", "NASDAQ")]
  [InlineData("NYSE", "NYSE")]
  [InlineData("  ARCA  ", "ARCA")]
  [InlineData(null, null)]
  [InlineData("   ", null)]
  public void NormalizeExchange_UppercasesAndTrims(string? input, string? expected) =>
    Assert.Equal(expected, BusinessQuantMappers.NormalizeExchange(input));

  [Theory]
  [InlineData("Equity", "Stock")]
  [InlineData("ETF", "Etf")]
  [InlineData("Fund", "Etf")]
  [InlineData("Unknown", null)]
  [InlineData("", null)]
  public void ToInstrumentType_MapsSecurityTypes(string securityType, string? expected) =>
    Assert.Equal(expected, BusinessQuantMappers.ToInstrumentType(securityType));

  [Fact]
  public void ToBusinessQuantInstrument_MapsUniverseRecord()
  {
    var instrument = new BusinessQuantUniverseRecord
    {
      SecurityType = "Equity",
      Ticker = "AAPL",
      Cik = 320193,
      Name = "Apple Inc.",
      NameShort = "Apple",
      Sector = "Technology",
      Industry = "Consumer Electronics",
      Exchange = "NASDAQ"
    }.ToBusinessQuantInstrument();

    Assert.Equal("AAPL", instrument.Ticker);
    Assert.Equal("NASDAQ", instrument.ExchangeName);
    Assert.Equal("USD", instrument.Currency);
    Assert.Equal("Apple", instrument.Name);
    Assert.Equal("Technology", instrument.Sector);
    Assert.Equal("Stock", instrument.Type);
    Assert.Equal("businessquant", instrument.Source);
  }

  [Fact]
  public void ToBusinessQuantInstrument_PrefersFullName_WhenShortIsBlank()
  {
    var instrument = new BusinessQuantUniverseRecord
    {
      SecurityType = "ETF",
      Ticker = "SPY",
      Name = "SPDR S&P 500 ETF Trust",
      NameShort = "  ",
      Sector = "",
      Exchange = "NYSE"
    }.ToBusinessQuantInstrument();

    Assert.Equal("SPDR S&P 500 ETF Trust", instrument.Name);
    Assert.Null(instrument.Sector);
    Assert.Equal("Etf", instrument.Type);
  }

  [Theory]
  [InlineData(TimeFrame.Minute, "minute-bars")]
  [InlineData(TimeFrame.Day, "eod")]
  public void ToQuotesMode_MapsTimeFrames(TimeFrame timeFrame, string expected) =>
    Assert.Equal(expected, timeFrame.ToQuotesMode());

  [Theory]
  [InlineData(TimeFrame.Hour)]
  [InlineData(TimeFrame.Week)]
  [InlineData(TimeFrame.Month)]
  public void ToQuotesMode_UnsupportedTimeFrames_Throw(TimeFrame timeFrame) =>
    Assert.Throws<ArgumentOutOfRangeException>(() => timeFrame.ToQuotesMode());

  [Fact]
  public void ToCandle_MapsQuoteBar_SummerOffset()
  {
    // 2026-08-16 09:30 US Eastern = EDT (UTC-4)
    var candle = new BusinessQuantQuoteBar
    {
      Date = "2026-08-16 09:30:00",
      Open = 210m,
      High = 212m,
      Low = 209m,
      Close = 211.5m,
      Volume = 123456
    }.ToCandle("AAPL", TimeFrame.Minute, "NASDAQ", true);

    Assert.Equal("AAPL", candle.Ticker);
    Assert.Equal("NASDAQ", candle.ExchangeName);
    Assert.Equal(TimeFrame.Minute, candle.TimeFrame);
    Assert.Equal(new DateTime(2026, 8, 16, 13, 30, 0), candle.Timestamp);
    Assert.Equal(210m, candle.Open);
    Assert.Equal(212m, candle.High);
    Assert.Equal(209m, candle.Low);
    Assert.Equal(211.5m, candle.Close);
    Assert.Equal(123456m, candle.Volume);
    Assert.True(candle.IsNotEmpty);
    Assert.True(candle.IsCompleted);
  }

  [Fact]
  public void ToCandle_MapsQuoteBar_WinterOffset()
  {
    // 2026-01-16 09:30 US Eastern = EST (UTC-5)
    var candle = new BusinessQuantQuoteBar
    {
      Date = "2026-01-16 09:30:00",
      Open = 1m,
      High = 2m,
      Low = 0.5m,
      Close = 1.5m,
      Volume = 10
    }.ToCandle("AAPL", TimeFrame.Minute, "NASDAQ", false);

    Assert.Equal(new DateTime(2026, 1, 16, 14, 30, 0), candle.Timestamp);
    Assert.False(candle.IsCompleted);
  }
}
