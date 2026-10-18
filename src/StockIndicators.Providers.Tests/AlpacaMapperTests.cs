using Alpaca.Markets;
using Moq;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Alpaca.Mappers;
using Xunit;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Tests;

public class AlpacaMapperTests
{
  [Theory]
  [InlineData(AssetClass.UsEquity, "Stock")]
  [InlineData(AssetClass.Crypto, "Crypto")]
  public void ToInstrumentType_MapsAssetClasses(AssetClass assetClass, string? expected) =>
    Assert.Equal(expected, assetClass.ToInstrumentType());

  [Fact]
  public void ToAlpacaInstrument_MapsAssetFields()
  {
    var asset = new Mock<IAsset>();
    asset.SetupGet(a => a.Symbol).Returns("AAPL");
    asset.SetupGet(a => a.Exchange).Returns(Exchange.Nasdaq);
    asset.SetupGet(a => a.Name).Returns("Apple Inc.");
    asset.SetupGet(a => a.Class).Returns(AssetClass.UsEquity);

    var instrument = asset.Object.ToAlpacaInstrument();

    Assert.Equal("AAPL", instrument.Ticker);
    Assert.Equal("NASDAQ", instrument.ExchangeName);
    Assert.Equal("USD", instrument.Currency);
    Assert.Equal("Apple Inc.", instrument.Name);
    Assert.Equal("Stock", instrument.Type);
    Assert.Equal("alpaca", instrument.Source);
  }

  [Theory]
  [InlineData(Exchange.Nasdaq, "NASDAQ")]
  [InlineData(Exchange.Nyse, "NYSE")]
  [InlineData(Exchange.Arca, "ARCA")]
  [InlineData(Exchange.NyseArca, "ARCA")]
  [InlineData(Exchange.Bats, "BATS")]
  [InlineData(Exchange.Amex, "AMEX")]
  [InlineData(Exchange.Otc, "OTC")]
  [InlineData(Exchange.Iex, "IEX")]
  [InlineData(Exchange.NyseMkt, "NYSE MKT")]
  public void ToExchangeName_MapsAllUsExchanges(Exchange exchange, string expected) =>
    Assert.Equal(expected, exchange.ToExchangeName());

  [Theory]
  [InlineData(TimeFrame.Minute)]
  [InlineData(TimeFrame.Hour)]
  [InlineData(TimeFrame.Day)]
  [InlineData(TimeFrame.Week)]
  [InlineData(TimeFrame.Month)]
  public void ToBarTimeFrame_MapsAllTimeFrames(TimeFrame timeFrame)
  {
    var expected = timeFrame switch
    {
      TimeFrame.Minute => BarTimeFrame.Minute,
      TimeFrame.Hour => BarTimeFrame.Hour,
      TimeFrame.Day => BarTimeFrame.Day,
      TimeFrame.Week => BarTimeFrame.Week,
      TimeFrame.Month => BarTimeFrame.Month,
      _ => throw new ArgumentOutOfRangeException(nameof(timeFrame))
    };

    Assert.Equal(expected, timeFrame.ToBarTimeFrame());
  }

  [Fact]
  public void ToCandle_MapsBarFields()
  {
    var bar = new FakeBar
    {
      Symbol = "AAPL",
      TimeUtc = new DateTime(2026, 8, 16, 13, 30, 0),
      Open = 10.5m,
      High = 11m,
      Low = 10m,
      Close = 10.9m,
      Volume = 123456,
      TradeCount = 789
    };

    var candle = bar.ToCandle(TimeFrame.Minute, "NASDAQ");

    Assert.Equal("AAPL", candle.Ticker);
    Assert.Equal("NASDAQ", candle.ExchangeName);
    Assert.Equal(TimeFrame.Minute, candle.TimeFrame);
    Assert.Equal(10.5m, candle.Open);
    Assert.Equal(11m, candle.High);
    Assert.Equal(10m, candle.Low);
    Assert.Equal(10.9m, candle.Close);
    Assert.Equal(123456m, candle.Volume);
    Assert.True(candle.IsNotEmpty);
    Assert.True(candle.IsCompleted);
  }

  private sealed class FakeBar : IBar
  {
    public string Symbol { get; init; } = null!;
    public DateTime TimeUtc { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public decimal Vwap { get; init; }
    public ulong TradeCount { get; init; }
  }
}
