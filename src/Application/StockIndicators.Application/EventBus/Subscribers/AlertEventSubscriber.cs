using Serilog;
using StockIndicators.Contracts.Indicators;

namespace StockIndicators.Application.EventBus.Subscribers;

/// <summary>
/// Consumes AlertEvent in the monolith.
/// Current notification flow is through AlertDispatcher → channel senders (Telegram, Web Push).
/// After Notification Service split: route to Telegram/Email/Push channels.
/// </summary>
public sealed class AlertEventSubscriber : IEventSubscriber<AlertEvent>
{
  private readonly ILogger _logger;

  public AlertEventSubscriber(ILogger logger) => _logger = logger.ForContext<AlertEventSubscriber>();

  public Task HandleAsync(AlertEvent @event, CancellationToken cancellationToken = default)
  {
    _logger.Information(
      "AlertEvent received: {InstrumentTicker} {IndicatorName} for subscription {SubscriptionId}",
      @event.InstrumentTicker, @event.IndicatorName, @event.SubscriptionId);
    return Task.CompletedTask;
  }
}
