namespace StockIndicators.Application.EventBus;

/// <summary>
/// Abstraction for publishing domain events.
/// In-memory implementation uses .NET events.
/// </summary>
public interface IEventBus
{
  Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull;
}
