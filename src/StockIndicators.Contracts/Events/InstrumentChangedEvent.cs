namespace StockIndicators.Contracts.Events;

/// <summary>
/// Published when a new instrument subscription is created or removed.
/// Consumed by Candle Ingestion to subscribe/unsubscribe from data providers.
/// </summary>
public record InstrumentChangedEvent(
  string Ticker,
  string? ExchangeName,
  InstrumentAction Action
);

public enum InstrumentAction
{
  Subscribed = 0,
  Unsubscribed = 1
}
