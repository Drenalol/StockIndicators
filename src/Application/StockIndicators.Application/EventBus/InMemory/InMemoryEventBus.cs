using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace StockIndicators.Application.EventBus.InMemory;

/// <summary>
/// In-process event bus using .NET events for subscriber management.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger _logger;
  private readonly ConcurrentDictionary<Type, object> _subscribers = new();

  public InMemoryEventBus(IServiceProvider serviceProvider, ILogger logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull
  {
    var eventType = typeof(TEvent);
    var handlerType = typeof(IEventSubscriber<>).MakeGenericType(eventType);

    if (!_subscribers.TryGetValue(handlerType, out var handlersBox))
    {
      using var scope = _serviceProvider.CreateScope();
      var resolvedHandlers = scope.ServiceProvider.GetKeyedServices<IEventSubscriber<TEvent>>(eventType).ToList();
      _subscribers[handlerType] = handlersBox = resolvedHandlers;
    }

    var handlers = (IReadOnlyList<IEventSubscriber<TEvent>>)handlersBox;

    if (handlers.Count == 0)
    {
      _logger.Debug("No subscribers for {EventType}", eventType.Name);
      return;
    }

    _logger.Debug("Publishing {EventType} to {Count} subscribers", eventType.Name, handlers.Count);

    await Task.WhenAll(handlers.Select(handler => handler.HandleAsync(@event, cancellationToken)));
  }

  public void ClearSubscribers() => _subscribers.Clear();
}
