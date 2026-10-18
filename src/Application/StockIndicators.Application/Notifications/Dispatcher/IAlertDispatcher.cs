using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Notifications.Dispatcher;

public interface IAlertDispatcher
{
  /// <summary>
  /// Queues one alert for the subscription's enabled outputs. <paramref name="instrumentTicker"/>
  /// and <paramref name="exchangeName"/> identify the instrument the alert fired on (the rule
  /// condition that triggered, not a property of the subscription itself).
  /// </summary>
  void AddAlert(
    Subscription subscription,
    string instrumentTicker,
    string? exchangeName,
    string indicatorName,
    string alertText,
    Guid triggerId,
    byte[]? imagePng,
    DateTime triggeredAt
  );

  Task FlushAsync(CancellationToken cancellationToken = default);
}
