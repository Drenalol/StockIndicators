using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Subscriptions.CreateSubscription;

public record SubscriptionOutputRequest(
  NotificationChannel Channel,
  Guid? NotificationTemplateId,
  Guid? PushSubscriptionId,
  long? ChatId,
  string? Target
);
