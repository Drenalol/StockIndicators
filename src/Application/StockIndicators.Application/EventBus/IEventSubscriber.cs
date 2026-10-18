namespace StockIndicators.Application.EventBus;

/// <summary>
/// Handler for events published through IEventBus.
/// Each event type can have multiple subscribers.
/// </summary>
public interface IEventSubscriber<in TEvent> where TEvent : notnull
{
  Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
