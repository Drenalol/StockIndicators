using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Candles;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Application.EventBus;
using StockIndicators.Application.Subscriptions.CheckSubscriptions;
using StockIndicators.Components.Utils;
using StockIndicators.Contracts.Candles;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Infrastructure.Domain.Candles;

public sealed class CandleProvider : ICandleProvider, IDisposable
{
  private readonly IEventBus _eventBus;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IChartLiveNotifier _chartLiveNotifier;
  private readonly ILogger _logger;

  private readonly BatchBlock<Candle> _batchBlock;
  private readonly Timer _trigger;
  private readonly IEnumerable<IDisposable> _subscriptionBlocks;
  private readonly TransformBlock<IEnumerable<Candle>, IEnumerable<Candle>> _reduceBlock;
  private readonly ActionBlock<IEnumerable<Candle>> _finalizerBlock;

  public CandleProvider(IOptions<CandleBufferOptions> options, IEventBus eventBus, IServiceScopeFactory scopeFactory, IChartLiveNotifier chartLiveNotifier, ILogger logger)
  {
    _eventBus = eventBus;
    _scopeFactory = scopeFactory;
    _chartLiveNotifier = chartLiveNotifier;
    _logger = logger;
    _batchBlock = new BatchBlock<Candle>(int.MaxValue);
    _reduceBlock = new TransformBlock<IEnumerable<Candle>, IEnumerable<Candle>>(candles => CandleReducer.Reduce(candles, _logger));
    _finalizerBlock = new ActionBlock<IEnumerable<Candle>>(CandleActionAsync);
    _subscriptionBlocks =
    [
      _batchBlock.LinkTo(_reduceBlock),
      _reduceBlock.LinkTo(_finalizerBlock)
    ];

    var now = DateTime.UtcNow;
    _trigger = new Timer(JobCallback, _batchBlock, now.AddSeconds(59 - now.Second) - now, TimeSpan.FromSeconds(options.Value.BatchTriggerDelaySec));

    void JobCallback(object? state) => ((BatchBlock<Candle>)(state ?? throw new ArgumentNullException(nameof(state)))).TriggerBatch();
  }

  public Task<bool> PushAsync(Candle candle, CancellationToken cancellationToken = default) => _batchBlock.SendAsync(candle, cancellationToken);

  private async Task CandleActionAsync(IEnumerable<Candle> candles)
  {
    candles = candles.AsArray();
    BusinessMetrics.CandlesFinalizedCount(((Candle[])candles).Length);
    var stopwatch = Stopwatch.StartNew();

    try
    {
      // Publish to Event Bus
      var candleBatchEvent = new CandleBatchEvent(
        DateTime.UtcNow,
        candles.ToDtos()
      );
      await _eventBus.PublishAsync(candleBatchEvent);

      // MediatR commands for in-process flow (UpdateCache + CheckSubscriptions)
      // Run in a scope since CandleProvider is Singleton and ActionBlock runs outside request scope
      using var scope = _scopeFactory.CreateScope();
      var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
      await scopedMediator.Publish(new UpdateCacheCommand(candles));

      // The cache now holds the fresher candles: nudge the open chart pages of exactly
      // these instruments (the notifier itself throttles per instrument and never throws)
      await _chartLiveNotifier.NotifyChangedAsync(
        candles.Select(candle => new InstrumentKey(candle.Ticker, candle.ExchangeName)));

      await scopedMediator.Publish(new CheckSubscriptionsCommand(candles.Select(candle => new InstrumentKey(candle.Ticker, candle.ExchangeName))));
    }
    catch (Exception e)
    {
      _logger.Error(e, "FinalizerBlock failed");
    }
    finally
    {
      BusinessMetrics.FinalizerDurationSeconds(stopwatch.Elapsed.TotalSeconds);
    }
  }

  public void Dispose()
  {
    _trigger.Dispose();

    foreach (var subscriptionBlock in _subscriptionBlocks)
      subscriptionBlock.Dispose();

    _finalizerBlock.Complete();
    _reduceBlock.Complete();
    _batchBlock.Complete();
  }
}

public static class CandleReducer
{
  public static IEnumerable<Candle> Reduce(IEnumerable<Candle> candles, ILogger? logger = null) =>
    candles
      .OrderByDescending(candle => candle.Timestamp)
      .Distinct(CandleReducerComparer.Instance)
      .GroupBy(
        candle => (candle.Ticker, candle.ExchangeName),
        (key, grouping) =>
        {
          var (ticker, exchangeName) = key;
          grouping = grouping.AsArray();
          var candleByMaxDate = grouping.MaxBy(candle => candle.Timestamp)!;
          logger?.Verbose("Reduced {Count} candles for {Ticker} ({Exchange}) by {DateTime}", ((Candle[])grouping).Length, ticker, exchangeName, candleByMaxDate.Timestamp);
          return candleByMaxDate;
        }
      );
}
