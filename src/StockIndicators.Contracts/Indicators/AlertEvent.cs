namespace StockIndicators.Contracts.Indicators;

/// <summary>
/// Event published by Alert Engine when an indicator triggers.
/// Consumed by Notification service.
/// </summary>
public record AlertEvent(
  Guid SubscriptionId,
  Guid UserId,
  string InstrumentTicker,
  string IndicatorName,
  string AlertText,
  DateTime TriggeredAt
);
