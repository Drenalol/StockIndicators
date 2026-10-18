namespace StockIndicators.Application.Subscriptions.GetTriggerHistory;

public record TriggerHistoryDto(
  string Id,
  string TriggerValue,
  DateTime TriggeredAt,
  DateTime? TriggeredCandleAt,
  IReadOnlyList<TriggerDeliveryDto> Deliveries
);

/// <summary>
/// Per-channel delivery result: channel name, delivery status
/// (Sent/Failed/Skipped), the error when not delivered and the attempt time (UTC).
/// </summary>
public record TriggerDeliveryDto(string Channel, string Status, string? Error, DateTime SentAt);
