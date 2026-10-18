using Serilog;
using StockIndicators.Contracts.Events;

namespace StockIndicators.Application.EventBus.Subscribers;

/// <summary>
/// Consumes InstrumentChangedEvent in the monolith.
/// Current instrument tracking is through IInstrumentProvider reactive subjects.
/// After Candle Ingestion split: subscribe/unsubscribe from external data providers.
/// </summary>
public sealed class InstrumentChangedEventSubscriber : IEventSubscriber<InstrumentChangedEvent>
{
  private readonly ILogger _logger;

  public InstrumentChangedEventSubscriber(ILogger logger) => _logger = logger.ForContext<InstrumentChangedEventSubscriber>();

  public Task HandleAsync(InstrumentChangedEvent @event, CancellationToken cancellationToken = default)
  {
    _logger.Information(
      "InstrumentChangedEvent received: {Action} {Ticker}",
      @event.Action, @event.Ticker);
    return Task.CompletedTask;
  }
}
