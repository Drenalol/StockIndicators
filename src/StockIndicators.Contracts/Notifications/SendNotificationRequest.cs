namespace StockIndicators.Contracts.Notifications;

/// <summary>
/// Request sent to Notification service by Alert Engine.
/// </summary>
public record SendNotificationRequest(
  string Channel,
  string RecipientId,
  string Subject,
  string Body
);
