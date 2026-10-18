namespace StockIndicators.Contracts.Candles;

/// <summary>
/// Event published by Candle Ingestion service when a batch of candles
/// is received and deduplicated. Consumed by Candle Store and Alert Engine.
/// </summary>
public record CandleBatchEvent(
  DateTime Timestamp,
  CandleDto[] Candles
);
