namespace StockIndicators.Application.Subscriptions.GetSubscription;

public class SubscriptionOutputDto
{
  public Guid Id { get; set; }
  public Guid SubscriptionId { get; set; }
  public string Channel { get; set; } = null!;
  public Guid? NotificationTemplateId { get; set; }
  public Guid? PushSubscriptionId { get; set; }
  public long? ChatId { get; set; }

  /// <summary>Destination URL (Webhook/Discord) or E.164 phone (WhatsApp).</summary>
  public string? Target { get; set; }

  public bool IsEnabled { get; set; }
}
