using System.Text.Json;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Binance.Mappers;
using StockIndicators.Providers.Binance.Schema;
using Xunit;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Tests;

public class BinanceMapperTests
{
  [Fact]
  public void ToBinanceInstrument_MapsSymbol()
  {
    var instrument = new BinanceSymbol
    {
      Symbol = "BTCUSDT",
      Status = "TRADING",
      BaseAsset = "BTC",
      QuoteAsset = "USDT",
      IsSpotTradingAllowed = true
    }.ToBinanceInstrument();

    Assert.Equal("BTCUSDT", instrument.Ticker);
    Assert.Equal("BINANCE", instrument.ExchangeName);
    Assert.Equal("USDT", instrument.Currency);
    Assert.Equal("BTC/USDT", instrument.Name);
    Assert.Equal("Crypto", instrument.Type);
    Assert.Equal("binance", instrument.Source);
  }

  [Theory]
  [InlineData(TimeFrame.Minute, "1m")]
  [InlineData(TimeFrame.Hour, "1h")]
  [InlineData(TimeFrame.Day, "1d")]
  [InlineData(TimeFrame.Week, "1w")]
  [InlineData(TimeFrame.Month, "1M")]
  public void ToKlineInterval_MapsAllTimeFrames(TimeFrame timeFrame, string expected) =>
    Assert.Equal(expected, timeFrame.ToKlineInterval());

  [Fact]
  public void ToCandle_MapsClosedKlineRow()
  {
    var openTime = new DateTimeOffset(2025, 7, 16, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    var closeTime = openTime + 59_999;
    var kline = MakeKline(openTime, closeTime);

    var nowUtc = new DateTime(2025, 7, 16, 9, 0, 0, DateTimeKind.Utc);
    var candle = kline.ToCandle("BTCUSDT", TimeFrame.Minute, "BINANCE", nowUtc);

    Assert.Equal("BTCUSDT", candle.Ticker);
    Assert.Equal("BINANCE", candle.ExchangeName);
    Assert.Equal(TimeFrame.Minute, candle.TimeFrame);
    Assert.Equal(new DateTime(2025, 7, 16, 8, 0, 0), candle.Timestamp);
    Assert.Equal(0.01634790m, candle.Open);
    Assert.Equal(0.80000000m, candle.High);
    Assert.Equal(0.01575800m, candle.Low);
    Assert.Equal(0.01577100m, candle.Close);
    Assert.Equal(148976.11427815m, candle.Volume);
    Assert.True(candle.IsNotEmpty);
    Assert.True(candle.IsCompleted);
  }

  [Fact]
  public void ToCandle_StillFormingKline_IsNotCompleted()
  {
    var openTime = new DateTimeOffset(2025, 7, 16, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    var closeTime = openTime + 60_000;
    var kline = MakeKline(openTime, closeTime);

    var nowUtc = new DateTime(2025, 7, 16, 8, 0, 30, DateTimeKind.Utc);
    var candle = kline.ToCandle("ETHUSDT", TimeFrame.Minute, "BINANCE", nowUtc);

    Assert.False(candle.IsCompleted);
  }

  private static JsonElement[] MakeKline(long openTime, long closeTime) =>
    System.Text.Json.JsonSerializer.SerializeToElement(new object[]
    {
      openTime,
      "0.01634790",
      "0.80000000",
      "0.01575800",
      "0.01577100",
      "148976.11427815",
      closeTime,
      "2434.19055334",
      308,
      "1756.87402397",
      "28.46694368",
      17928899.62484339
    }).EnumerateArray().ToArray();
}
