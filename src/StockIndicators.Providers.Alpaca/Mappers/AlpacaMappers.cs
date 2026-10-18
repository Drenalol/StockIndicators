using Alpaca.Markets;
using StockIndicators.Domain.Models.Candles;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Alpaca.Mappers;

public static class AlpacaMappers
{
  public static AlpacaInstrument ToAlpacaInstrument(this IAsset asset) =>
    new(asset.Symbol, asset.Exchange.ToExchangeName(), "USD", asset.Name, asset.Class.ToInstrumentType());

  public static string? ToInstrumentType(this AssetClass assetClass) => assetClass switch
  {
    AssetClass.UsEquity => "Stock",
    AssetClass.Crypto => "Crypto",
    _ => null
  };

  public static string? ToExchangeName(this Exchange exchange) => exchange switch
  {
    Exchange.Nasdaq => "NASDAQ",
    Exchange.Nyse => "NYSE",
    Exchange.Arca or Exchange.NyseArca => "ARCA",
    Exchange.Bats => "BATS",
    Exchange.Amex => "AMEX",
    Exchange.Otc => "OTC",
    Exchange.Iex => "IEX",
    Exchange.NyseMkt => "NYSE MKT",
    Exchange.Crypto => "CRYPTO",
    _ => null
  };

  public static BarTimeFrame ToBarTimeFrame(this TimeFrame timeFrame) => timeFrame switch
  {
    TimeFrame.Minute => BarTimeFrame.Minute,
    TimeFrame.Hour => BarTimeFrame.Hour,
    TimeFrame.Day => BarTimeFrame.Day,
    TimeFrame.Week => BarTimeFrame.Week,
    TimeFrame.Month => BarTimeFrame.Month,
    _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
  };

  public static Candle ToCandle(this IBar bar, TimeFrame timeFrame, string? exchangeName) =>
    new(bar.Symbol, bar.TimeUtc, timeFrame, bar.Open, bar.High, bar.Low, bar.Close, (decimal)bar.Volume, bar.TradeCount > 0, true, exchangeName);
}
