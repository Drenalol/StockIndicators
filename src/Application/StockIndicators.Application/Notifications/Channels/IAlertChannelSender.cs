using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Notifications.Channels;

public interface IAlertChannelSender
{
  NotificationChannel Channel { get; }

  /// <summary>
  /// Delivers the batch and reports one <see cref="DeliveryOutcome"/> per batch item
  /// (the dispatcher records the delivery rows and fills in missing outcomes itself).
  /// Throwing marks every item of the batch as failed.
  /// </summary>
  Task<IReadOnlyList<DeliveryOutcome>> SendAsync(AlertBatch batch, CancellationToken cancellationToken);
}
