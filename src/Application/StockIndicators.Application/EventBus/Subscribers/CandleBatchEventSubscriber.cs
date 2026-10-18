using Serilog;
using StockIndicators.Contracts.Candles;

namespace StockIndicators.Application.EventBus.Subscribers;

/// <summary>
/// Consumes CandleBatchEvent in the monolith.
/// Current data flow is through MediatR (UpdateCacheCommand, CheckSubscriptionsCommand).
/// After Candle Store / Alert Engine split: persist to DB, trigger indicator evaluation.
/// </summary>
public sealed class CandleBatchEventSubscriber : IEventSubscriber<CandleBatchEvent>
{
  private readonly ILogger _logger;

  public CandleBatchEventSubscriber(ILogger logger) => _logger = logger.ForContext<CandleBatchEventSubscriber>();

  public Task HandleAsync(CandleBatchEvent @event, CancellationToken cancellationToken = default)
  {
    _logger.Information(
      "CandleBatchEvent received: {CandleCount} candles at {Timestamp}",
      @event.Candles.Length, @event.Timestamp);
    return Task.CompletedTask;
  }
}
