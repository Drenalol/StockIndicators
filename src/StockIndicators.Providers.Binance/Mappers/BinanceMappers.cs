using System.Globalization;
using System.Text.Json;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Binance.Schema;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.Binance.Mappers;

public static class BinanceMappers
{
  public const string ExchangeName = "BINANCE";

  public static BinanceInstrument ToBinanceInstrument(this BinanceSymbol symbol) =>
    new(symbol.Symbol, ExchangeName, symbol.QuoteAsset, $"{symbol.BaseAsset}/{symbol.QuoteAsset}");

  public static string ToKlineInterval(this TimeFrame timeFrame) => timeFrame switch
  {
    TimeFrame.Minute => "1m",
    TimeFrame.Hour => "1h",
    TimeFrame.Day => "1d",
    TimeFrame.Week => "1w",
    TimeFrame.Month => "1M",
    _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
  };

  /// <summary>
  /// Kline array (12 fields): openTime, open, high, low, close, volume,
  /// closeTime, quoteVolume, trades, takerBase, takerQuote, ignore.
  /// openTime/closeTime/trades/ignore arrive as JSON numbers, the rest as strings.
  /// </summary>
  public static Candle ToCandle(this JsonElement[] kline, string ticker, TimeFrame timeFrame, string? exchangeName, DateTime nowUtc)
  {
    var openTime = DateTimeOffset.FromUnixTimeMilliseconds(kline[0].GetInt64()).UtcDateTime;
    var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(kline[6].GetInt64()).UtcDateTime;

    return new Candle(
      ticker,
      openTime,
      timeFrame,
      Parse(kline[1]),
      Parse(kline[2]),
      Parse(kline[3]),
      Parse(kline[4]),
      Parse(kline[5]),
      true,
      closeTime <= nowUtc,
      exchangeName
    );

    static decimal Parse(JsonElement value) => decimal.Parse(value.GetString()!, CultureInfo.InvariantCulture);
  }
}
