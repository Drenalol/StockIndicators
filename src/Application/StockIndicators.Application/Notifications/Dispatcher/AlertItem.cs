namespace StockIndicators.Application.Notifications.Dispatcher;

public sealed record AlertItem(
  Guid UserId,
  Guid SubscriptionId,
  string InstrumentTicker,
  string? ExchangeName,
  string IndicatorName,
  string AlertText,
  DateTime TriggeredAt,
  Guid TriggerId,
  byte[]? ImagePng
);
