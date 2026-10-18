using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.PushSubscriptions;

namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// A single alert output of a subscription: delivery channel, its notification template and (for push channels) the target.
/// </summary>
public class SubscriptionOutput : Entity<Guid>
{
  public Guid SubscriptionId { get; set; }
  public NotificationChannel Channel { get; set; }
  public Guid? NotificationTemplateId { get; set; }
  public Guid? PushSubscriptionId { get; set; }
  /// <summary>
  /// Destination Telegram chat id (for Channel = Telegram).
  /// </summary>
  public long? ChatId { get; set; }

  /// <summary>
  /// Destination for URL/phone-addressed channels (for Channel = Webhook/Discord:
  /// the destination URL; for Channel = WhatsApp: an E.164 phone number).
  /// </summary>
  public string? Target { get; set; }

  public bool IsEnabled { get; set; } = true;

  public NotificationTemplate? NotificationTemplate { get; set; }
  public PushSubscription? PushSubscription { get; set; }
}
