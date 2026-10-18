using System.ComponentModel.DataAnnotations;
using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Controllers;

public class SubscriptionOutputRequest
{
  [Required]
  public NotificationChannel Channel { get; set; }

  public Guid? NotificationTemplateId { get; set; }
  public Guid? PushSubscriptionId { get; set; }

  /// <summary>
  /// Destination Telegram chat id (Channel = Telegram). The account has at most one
  /// linked chat and every Telegram output is addressed to it, so the value is
  /// ignored — the server fills in the account's current chat.
  /// </summary>
  public long? ChatId { get; set; }

  /// <summary>
  /// Destination URL (Channel = Webhook/Discord) or E.164 phone number
  /// (Channel = WhatsApp). Ignored for chat/push channels.
  /// </summary>
  public string? Target { get; set; }
}
