using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// Per-channel delivery result of a single trigger: when the channel was attempted,
/// whether it succeeded and the error text when it did not.
/// </summary>
public class TriggerDelivery : Entity<Guid>
{
  public Guid TriggerHistoryId { get; set; }
  public NotificationChannel Channel { get; set; }
  public DeliveryStatus Status { get; set; }

  /// <summary>
  /// Failure or skip reason (server-authored, technical). Null for delivered alerts.
  /// </summary>
  public string? Error { get; set; }

  public DateTime SentAt { get; set; }
}
