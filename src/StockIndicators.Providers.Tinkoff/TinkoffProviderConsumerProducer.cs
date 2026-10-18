using System.Net.WebSockets;
using System.Reactive;
using System.Reactive.Linq;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Events;
using StockIndicators.Abstract;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Components.Utils;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Tinkoff.Internal;
using StockIndicators.Providers.Tinkoff.Mappers;
using Tinkoff.InvestApi.V1;

namespace StockIndicators.Providers.Tinkoff;

public enum CandleAction
{
  Subscribe,
  Unsubscribe
}

public class TinkoffProviderConsumerProducer : ConsumerProviderBase, ICandleSource, IDisposable
{
  private HashSet<ICustomConnection> Connections
  {
    get
    {
      if (_connections == null)
        throw new ArgumentNullException(nameof(_connections), "Connections is not defined");

      return _connections;
    }
  }

  private IEnumerable<IInstrument> Instruments
  {
    get
    {
      if (_instruments == null)
        throw new ArgumentNullException(nameof(_instruments), "Instruments is not defined");

      return _instruments;
    }
  }

  private readonly TinkoffProvider _provider;
  private readonly TinkoffProviderClient _tinkoffProviderClient;
  private readonly TinkoffProviderOptions _tinkoffOptions;
  private readonly ICandleProvider _buffer;
  private readonly IInstrumentProvider _instrumentProvider;
  private readonly CustomConnectionFactory _connectionFactory;
  private readonly SemaphoreSlim _lock;
  private readonly AsyncRetryPolicy _retryPolicy;
  private HashSet<ICustomConnection>? _connections;
  private HashSet<IInstrument>? _instruments;
  private volatile bool _stopping;
  private readonly IEnumerable<IDisposable> _subscriptions;

  public TinkoffProviderConsumerProducer(
    TinkoffProvider provider,
    TinkoffProviderClient tinkoffProviderClient,
    IOptions<TinkoffProviderOptions> tinkoffOptions,
    ICandleProvider buffer,
    IInstrumentProvider instrumentProvider,
    CustomConnectionFactory connectionFactory,
    ILogger logger
  ) : base(provider.ProviderCode, logger)
  {
    _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    _tinkoffProviderClient = tinkoffProviderClient ?? throw new ArgumentNullException(nameof(tinkoffProviderClient));
    _tinkoffOptions = tinkoffOptions.Value ?? throw new ArgumentNullException(nameof(tinkoffOptions));
    _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    _instrumentProvider = instrumentProvider;
    _connectionFactory = connectionFactory;

    _subscriptions =
    [
      instrumentProvider.OnAdded
        .SelectMany(CandleSubscribeAsync)
        .Subscribe(),
      instrumentProvider.OnRemoved
        .SelectMany(CandleUnsubscribeAsync)
        .Subscribe()
    ];
    _lock = new SemaphoreSlim(1, 1);
    _retryPolicy = Policy
      .Handle<WebSocketException>()
      .WaitAndRetryForeverAsync(
        (_, _) => TimeSpan.FromSeconds(5),
        (
          exception,
          retry,
          time,
          context
        ) =>
        {
          var retryTime = time.ToString();
          const string message = "Request failed after {Retry} retry, waiting {RetryTime} before retry. Context: {@Context}";

          Logger.Write(
            retry < 50 ? LogEventLevel.Warning : LogEventLevel.Error,
            exception,
            message,
            retry,
            retryTime,
            context
          );
        }
      );
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _connections = [];

    _ = InternalStartAsync(cancellationToken);
    return Task.CompletedTask;
  }

  private async Task InternalStartAsync(CancellationToken cancellationToken)
  {
    await EnsureInstrumentCacheUpdatedAsync(cancellationToken);

    // Tinkoff's load balancer resets an idle market-data stream after ~1 minute
    // (investAPI#290), so with nobody watching a Tinkoff instrument the streams stay closed
    // entirely and the first OnAdded opens one; otherwise every kept-alive idle stream
    // restarts once a minute (reset, reconnect, full GetInstruments) forever.
    if (!SubscribableInstruments().Any())
    {
      Logger.Information("No Tinkoff instruments are watched, market data streams will start on demand");
      return;
    }

    var connections = await Enumerable
      .Range(0, _tinkoffOptions.ConnectionCount)
      .Select(_ => CreateConnectionAsync())
      .WhenAll();

    Connections.UnionWith(connections);

    foreach (var connection in connections)
      await CandleSubscribeAsync(connection, cancellationToken);
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _stopping = true;

    if (_connections != null)
    {
      foreach (var connection in _connections)
        ConnectionDispose(connection);

      _connections.Clear();
    }

    _lock.Dispose();
    return Task.CompletedTask;
  }

  private Task<ICustomConnection> CreateConnectionAsync()
  {
    var connection = _connectionFactory.Create();
    Logger.Debug("Connection was created with Id: {Id}", connection.Id.ToString());
    return Task.FromResult((ICustomConnection)connection);
  }

  /// <summary>
  /// Strict scoping: this consumer only handles instruments whose exchange is MOEX
  /// (Tinkoff universe). A null exchange (legacy subscription) is handled as before.
  /// </summary>
  private static bool IsTinkoffInstrument(InstrumentKey key) =>
    key.ExchangeName == null || string.Equals(key.ExchangeName, "MOEX", StringComparison.OrdinalIgnoreCase);

  private void ConnectionDispose(ICustomConnection connection)
  {
    Logger.Debug("Connection {Id} start disposing", connection.Id);

    if (_instruments != null)
      foreach (var instrument in Instruments.OfType<CachedTinkoffInstrument>().Where(instrument => instrument.ConnectionId == connection.Id))
    {
      instrument.ConnectionId = null;
      Logger.Debug("Connection {Id} unexpected unsubscribed from {Figi} {Ticker}", connection.Id, instrument.Ticker, instrument.Ticker);
    }

    connection.Dispose();
  }

  private void ConnectionClosed(ICustomConnection connection, CancellationToken cancellationToken)
  {
    BusinessMetrics.StreamReconnect(ProviderMetricLabel);
    Logger.Warning("Stream connection closed, connection {Id}", connection.Id);
    _ = RestartConnectionAsync(connection, cancellationToken);
  }

  private async Task RestartConnectionAsync(ICustomConnection brokenConnection, CancellationToken cancellationToken)
  {
    using var _ = await _lock.LockAsync(cancellationToken);
    ConnectionDispose(brokenConnection);
    Connections.Remove(brokenConnection);

    await EnsureInstrumentCacheUpdatedAsync(cancellationToken);

    // Every instrument this connection carried has since been unwatched: a recreated
    // stream would carry nothing and be reset by the load balancer a minute later, so it
    // is dropped and reopened by the next OnAdded instead.
    if (!SubscribableInstruments().Any())
    {
      Logger.Information("Connection {Id} closed with nothing left to subscribe, the stream is not recreated", brokenConnection.Id);
      return;
    }

    var context = CreateRetryContext(nameof(RestartConnectionAsync));
    context.Add("Id", brokenConnection.Id);

    var connection = await _retryPolicy.ExecuteAsync(_ => CreateConnectionAsync(), context);
    Connections.Add(connection);

    await CandleSubscribeAsync(connection, cancellationToken);
  }

  private async Task CandleSubscribeAsync(ICustomConnection connection, CancellationToken cancellationToken)
  {
    var maxTake = (int)Math.Min(300, Math.Ceiling(_instrumentProvider.Instruments.Count / (double)_tinkoffOptions.ConnectionCount));
    var context = CreateRetryContext(nameof(CandleSubscribeAsync));
    context.Add("ConnectionId", connection.Id);

    await StartReaderAsync(connection, cancellationToken);
    await CandleActionAsync(SubscribableInstruments().Take(maxTake).ToArray(), connection, context, CandleAction.Subscribe);
  }

  private IEnumerable<CachedTinkoffInstrument> SubscribableInstruments() =>
    Instruments
      .OfType<CachedTinkoffInstrument>()
      .Where(instrument => _instrumentProvider.Instruments.Contains(instrument.Ticker) && instrument.ConnectionId == null);

  private async Task CandleActionAsync(
    IReadOnlyCollection<CachedTinkoffInstrument> instruments,
    ICustomConnection connection,
    IDictionary<string, object> context,
    CandleAction action
  )
  {
    var marketDataRequest = new MarketDataRequest
    {
      SubscribeCandlesRequest = new SubscribeCandlesRequest
      {
        SubscriptionAction = action switch
        {
          CandleAction.Subscribe => SubscriptionAction.Subscribe,
          CandleAction.Unsubscribe => SubscriptionAction.Unsubscribe,
          _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        }
      }
    };

    var candleInstruments = instruments.Select(
      i => new CandleInstrument
      {
        // Tinkoff stream API requires FIGI; raw ticker is rejected with InstrumentNotFound
        InstrumentId = string.IsNullOrEmpty(i.Figi) ? i.Ticker : i.Figi,
        Interval = SubscriptionInterval.OneMinute
      }
    );

    marketDataRequest.SubscribeCandlesRequest.Instruments.AddRange(candleInstruments);

    await _retryPolicy.ExecuteAsync(_ => connection.Stream.RequestStream.WriteAsync(marketDataRequest), context);

    foreach (var instrument in instruments)
    {
      instrument.ConnectionId = action switch
      {
        CandleAction.Subscribe => connection.Id,
        CandleAction.Unsubscribe => null,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
      };

      Logger.Debug("Connection {Id} {Action} {Ticker} instrument", connection.Id, action.ToString(), instrument.Ticker);
    }

    Logger.Debug("Connection {Id} {Action} {Instruments} instruments", connection.Id, action.ToString(), instruments.Count);

    // The stream subscriptions this consumer currently carries
    BusinessMetrics.TrackedInstrumentsCount(ProviderMetricLabel, Instruments.OfType<CachedTinkoffInstrument>().Count(instrument => instrument.ConnectionId != null));
  }

  private async Task<Unit> CandleSubscribeAsync(InstrumentKey key)
  {
    if (!IsTinkoffInstrument(key))
      return Unit.Default;

    // The watch keys arrive with the hub/subscription casing (the provider registry pairs
    // them case-insensitively), so the stream instrument must be matched the same way
    var instrument = Instruments
      .OfType<CachedTinkoffInstrument>()
      .SingleOrDefault(instrument => string.Equals(instrument.Ticker, key.Ticker, StringComparison.OrdinalIgnoreCase) && instrument.ConnectionId == null);

    if (instrument == null)
      return Unit.Default;

    using var lockTaken = await _lock.LockAsync();

    var connection = await AcquireConnectionAsync();

    var context = CreateRetryContext(nameof(CandleSubscribeAsync));
    context.Add("ConnectionId", connection.Id);

    await CandleActionAsync([instrument], connection, context, CandleAction.Subscribe);

    return Unit.Default;
  }

  /// <summary>
  /// Least-loaded connection, under the lock. Streams are lazy: the first watched instrument
  /// opens a connection, and idle leftovers of load-balancer-reset streams (investAPI#290)
  /// are reused before any new one is created.
  /// </summary>
  private async Task<ICustomConnection> AcquireConnectionAsync()
  {
    var leastLoaded = Instruments
      .OfType<CachedTinkoffInstrument>()
      .Where(i => i.ConnectionId != null)
      .GroupBy(i => i.ConnectionId, (connectionId, instruments) => new { ConnectionId = connectionId, Count = instruments.Count() })
      .MinBy(group => group.Count);

    if (leastLoaded != null)
      return Connections.Single(connection => connection.Id == leastLoaded.ConnectionId);

    if (Connections.Count > 0)
      return Connections.First();

    var connection = await CreateConnectionAsync();
    await StartReaderAsync(connection, CancellationToken.None);
    Connections.Add(connection);

    return connection;
  }

  private async Task<Unit> CandleUnsubscribeAsync(InstrumentKey key)
  {
    if (!IsTinkoffInstrument(key))
      return Unit.Default;

    var instrument = Instruments
      .OfType<CachedTinkoffInstrument>()
      .SingleOrDefault(instrument => string.Equals(instrument.Ticker, key.Ticker, StringComparison.OrdinalIgnoreCase) && instrument.ConnectionId != null);

    if (instrument == null)
      return Unit.Default;

    var connection = Connections.SingleOrDefault(connection => connection.Id == instrument.ConnectionId);

    if (connection == null)
      return Unit.Default;

    using var lockTaken = await _lock.LockAsync();

    var context = CreateRetryContext(nameof(CandleUnsubscribeAsync));
    context.Add("ConnectionId", connection.Id);

    await CandleActionAsync([instrument], connection, context, CandleAction.Unsubscribe);

    return Unit.Default;
  }

  private async Task EnsureInstrumentCacheUpdatedAsync(CancellationToken cancellationToken)
  {
    var context = CreateRetryContext(nameof(EnsureInstrumentCacheUpdatedAsync));
    var actualInstruments = await _retryPolicy.ExecuteAsync(_ => _tinkoffProviderClient.GetInstrumentsAsync(cancellationToken), context);

    if (_instruments == null)
      _instruments = new HashSet<IInstrument>(ToCached(actualInstruments), InstrumentComparer.Instance);
    else
    {
      _instruments.IntersectWith(actualInstruments);
      var diff = actualInstruments.Except(_instruments, InstrumentComparer.Instance);
      _instruments.UnionWith(ToCached(diff));
    }

    static IEnumerable<IInstrument> ToCached(IEnumerable<IInstrument> items) => items.Select(instrument => (IInstrument)new CachedTinkoffInstrument(instrument.Ticker, instrument.ExchangeName, null, instrument.Currency, instrument.Name, instrument.Isin, instrument.Sector, instrument.Type) { Figi = instrument.Figi });
  }

  private Task StartReaderAsync(ICustomConnection connection, CancellationToken cancellationToken)
  {
    _ = ReaderAsync();
    return Task.CompletedTask;

    async Task ReaderAsync()
    {
      var healthCheck = TimeSpan.FromMinutes(5);
      using var cts = new CancellationTokenSource(healthCheck);

      // Single-shot: the timer tick and a stream fault must not restart the same connection twice,
      // and a stream faulted by StopAsync's dispose must not resurrect the connection.
      var restartTriggered = 0;
      await using var connectionDisposeRegister = cts.Token.Register(() =>
      {
        if (Interlocked.CompareExchange(ref restartTriggered, 1, 0) == 0 && !_stopping)
          ConnectionClosed(connection, cancellationToken);
      });

      try
      {
        await foreach (var response in connection.Stream.ResponseStream.ReadAllAsync(cts.Token))
        {
          ResetCancellationToken();
          OperateMarketDataResponse(response);
        }
      }
      catch (Exception e) when (e is not OperationCanceledException)
      {
        // StopAsync disposes the gRPC call while the reader is parked on MoveNext, and the
        // stream surfaces that as a Cancelled RpcException ("gRPC call disposed"), not an
        // OperationCanceledException — during shutdown this is expected, not a fault.
        if (_stopping)
          Logger.Debug(e, "Stream reader stopped on connection {Id}: app is shutting down", connection.Id);
        // The server reset the stream (Tinkoff's load balancer does this periodically, see
        // investAPI#290); waiting for the health-check tick would silently drop candles for
        // up to 5 minutes, so restart now.
        else
          Logger.Warning(e, "Stream reader faulted on connection {Id}, restarting now", connection.Id);
      }

      if (Interlocked.CompareExchange(ref restartTriggered, 1, 0) == 0 && !_stopping)
        ConnectionClosed(connection, cancellationToken);

      void ResetCancellationToken() => cts.CancelAfter(healthCheck);
    }
  }

  private async void OperateMarketDataResponse(MarketDataResponse marketDataResponse)
  {
    switch (marketDataResponse.PayloadCase)
    {
      case MarketDataResponse.PayloadOneofCase.Ping:
      {
        Logger.Debug("Ping: {Time}", marketDataResponse.Ping.Time.ToString());
        break;
      }
      case MarketDataResponse.PayloadOneofCase.SubscribeCandlesResponse:
      {
        foreach (var candlesSubscription in marketDataResponse.SubscribeCandlesResponse.CandlesSubscriptions)
          Logger.Debug("{Figi} candle subscription: {Status}", candlesSubscription.Ticker, candlesSubscription.SubscriptionStatus.ToString());

        break;
      }
      case MarketDataResponse.PayloadOneofCase.Candle:
      {
        var candle = marketDataResponse.Candle;
        var domainCandle = candle.ToDomain();
        await _buffer.PushAsync(domainCandle);
        BusinessMetrics.CandleReceived(ProviderMetricLabel, domainCandle.Timestamp);

        Logger.Verbose(
          "{Figi} O: {Open}, H: {High}, L: {Low}, C: {Close}, V: {Volume}",
          candle.Ticker,
          candle.Open,
          candle.High,
          candle.Low,
          candle.Close,
          candle.Volume
        );

        break;
      }
      default:
      {
        Logger.Warning("Not implemented event: {Event}, raw value: {@Raw}", marketDataResponse.PayloadCase.ToString(), marketDataResponse);
        break;
      }
    }
  }

  public void Dispose()
  {
    foreach (var subscription in _subscriptions)
      subscription.Dispose();
  }
}
