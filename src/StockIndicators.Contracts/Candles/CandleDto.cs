namespace StockIndicators.Contracts.Candles;

/// <summary>
/// Candle DTO shared between Candle Ingestion and Candle Store services.
/// </summary>
public record CandleDto(
  string Ticker,
  string? ExchangeName,
  DateTime Date,
  TimeFrame TimeFrame,
  decimal Open,
  decimal High,
  decimal Low,
  decimal Close,
  long Volume,
  bool IsCompleted
);
