using System.Globalization;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.BusinessQuant.Schema;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.BusinessQuant.Mappers;

public static class BusinessQuantMappers
{
  private const string UsEasternTimeZoneId = "America/New_York";
  private const string QuoteBarDateFormat = "yyyy-MM-dd HH:mm:ss";

  private static readonly TimeZoneInfo UsEastern = TimeZoneInfo.FindSystemTimeZoneById(UsEasternTimeZoneId);

  public static BusinessQuantInstrument ToBusinessQuantInstrument(this BusinessQuantUniverseRecord record) =>
    new(
      record.Ticker,
      NormalizeExchange(record.Exchange),
      "USD",
      string.IsNullOrWhiteSpace(record.NameShort) ? record.Name : record.NameShort,
      string.IsNullOrWhiteSpace(record.Sector) ? null : record.Sector,
      ToInstrumentType(record.SecurityType)
    );

  public static string? ToInstrumentType(string securityType) => securityType switch
  {
    "Equity" => "Stock",
    "ETF" or "Fund" => "Etf",
    _ => null
  };

  public static string? NormalizeExchange(string? exchange) =>
    string.IsNullOrWhiteSpace(exchange) ? null : exchange.Trim().ToUpperInvariant();

  public static string ToQuotesMode(this TimeFrame timeFrame) => timeFrame switch
  {
    TimeFrame.Minute => "minute-bars",
    TimeFrame.Day => "eod",
    _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
  };

  public static Candle ToCandle(this BusinessQuantQuoteBar bar, string ticker, TimeFrame timeFrame, string? exchangeName, bool isCompleted) =>
    bar.ToCandle(ticker, timeFrame, ToUtcDateTime(bar.Date), exchangeName, isCompleted);

  public static Candle ToCandle(this BusinessQuantQuoteBar bar, string ticker, TimeFrame timeFrame, DateTime timeUtc, string? exchangeName, bool isCompleted) =>
    new(
      ticker,
      timeUtc,
      timeFrame,
      bar.Open,
      bar.High,
      bar.Low,
      bar.Close,
      bar.Volume,
      true,
      isCompleted,
      exchangeName
    );

  /// <summary>
  /// Vendor timestamps carry no zone and are US Eastern local time
  /// (e.g. EOD records resolve to 16:00:00, the US market close).
  /// </summary>
  public static DateTime ToUtcDateTime(string date)
  {
    var local = DateTime.ParseExact(date, QuoteBarDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
    return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), UsEastern);
  }
}
