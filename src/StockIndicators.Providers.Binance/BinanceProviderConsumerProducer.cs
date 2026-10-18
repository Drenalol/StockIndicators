using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Refit;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Providers.Binance.Internal;
using StockIndicators.Providers.Binance.Mappers;
using StockIndicators.Providers.Base;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;

namespace StockIndicators.Providers.Binance;

/// <summary>
/// Polls the last two 1m klines per tracked pair (crypto is 24/7, no exchange session
/// constraint) and feeds the shared candle pipeline: the just-closed bar as completed
/// and the still-forming bar as not completed.
/// </summary>
public class BinanceProviderConsumerProducer : ConsumerProviderBase, ICandleSource, IDisposable
{
  private static readonly TimeSpan IpBanPause = TimeSpan.FromMinutes(15);

  private readonly BinanceProvider _provider;
  private readonly BinanceProviderClient _client;
  private readonly BinanceApiSelector _api;
  private readonly BinanceProviderOptions _options;
  private readonly ICandleProvider _candleBuffer;
  private readonly IInstrumentProvider _instrumentProvider;
  private readonly CancellationTokenSource _cts = new();
  private readonly SemaphoreSlim _lock = new(1, 1);
  // Claims arrive from the hub/subscription paths in different casings; Binance symbols
  // are case-insensitive, the set must pair add/remove regardless of the casing
  private readonly HashSet<string> _pairs = new(StringComparer.OrdinalIgnoreCase);
  private readonly IEnumerable<IDisposable> _subscriptions;
  private DateTime _pausedUntil;

  public BinanceProviderConsumerProducer(
    BinanceProvider provider,
    BinanceProviderClient client,
    BinanceApiSelector api,
    IOptions<BinanceProviderOptions> options,
    ICandleProvider candleBuffer,
    IInstrumentProvider instrumentProvider,
    ILogger logger
  ) : base(provider.ProviderCode, logger)
  {
    _provider = provider;
    _client = client;
    _api = api;
    _options = options.Value;
    _candleBuffer = candleBuffer;
    _instrumentProvider = instrumentProvider;

    _subscriptions =
    [
      instrumentProvider.OnAdded
        .SelectMany(TrackAsync)
        .Subscribe(),
      instrumentProvider.OnRemoved
        .SelectMany(UntrackAsync)
        .Subscribe()
    ];
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    // Pick up instruments added before this consumer started (subscription restore).
    foreach (var key in _instrumentProvider.Keys)
      _ = TrackAsync(key, CancellationToken.None);

    _ = PollingLoopAsync(_cts.Token);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _cts.Cancel();
    return Task.CompletedTask;
  }

  public void Dispose()
  {
    _cts.Dispose();
    _lock.Dispose();
    foreach (var subscription in _subscriptions)
      subscription.Dispose();
  }

  private async Task<Unit> TrackAsync(InstrumentKey key, CancellationToken cancellationToken)
  {
    if (key.ExchangeName != null && !string.Equals(key.ExchangeName, BinanceMappers.ExchangeName, StringComparison.OrdinalIgnoreCase))
      return Unit.Default;

    try
    {
      // Only poll pairs that actually exist on Binance; unknown pairs would 400 every cycle.
      // Poll under the exchange's canonical symbol: the klines endpoint rejects lowercase
      // (400 -1100), while watch keys arrive lower-cased from the hub/subscription matching.
      var instruments = await _client.GetInstrumentsAsync(cancellationToken);
      var match = instruments.FirstOrDefault(candidate => string.Equals(candidate.Ticker, key.Ticker, StringComparison.OrdinalIgnoreCase));

      if (match is null)
        return Unit.Default;

      await _lock.WaitAsync(cancellationToken);
      try
      {
        if (_pairs.Add(match.Ticker))
          Logger.Information("Binance tracking {Pair}", match.Ticker);
      }
      finally
      {
        _lock.Release();
      }
    }
    catch (Exception e)
    {
      Logger.Debug("Binance: {Pair} not trackable ({Reason})", key.Ticker, e.Message);
    }

    return Unit.Default;
  }

  private async Task<Unit> UntrackAsync(InstrumentKey key, CancellationToken cancellationToken)
  {
    if (key.ExchangeName != null && !string.Equals(key.ExchangeName, BinanceMappers.ExchangeName, StringComparison.OrdinalIgnoreCase))
      return Unit.Default;

    await _lock.WaitAsync(cancellationToken);
    try
    {
      if (_pairs.Remove(key.Ticker))
        Logger.Information("Binance untracked {Pair}", key.Ticker);
    }
    finally
    {
      _lock.Release();
    }

    return Unit.Default;
  }

  private async Task PollingLoopAsync(CancellationToken cancellationToken)
  {
    var interval = TimeSpan.FromSeconds(_options.CandlePollIntervalSeconds);

    while (!cancellationToken.IsCancellationRequested)
    {
      if (DateTime.UtcNow < _pausedUntil)
      {
        try
        {
          await Task.Delay(_pausedUntil - DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }

        continue;
      }

      var stopwatch = Stopwatch.StartNew();
      var failed = false;

      try
      {
        await PollCandlesAsync(cancellationToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (ApiException e) when (e.StatusCode == (HttpStatusCode)418)
      {
        failed = true;
        _pausedUntil = DateTime.UtcNow + IpBanPause;
        Logger.Error("Binance IP banned (418), pausing polling until {PausedUntil:O}", _pausedUntil);
      }
      catch (Exception e)
      {
        failed = true;
        Logger.Warning(e, "Binance polling cycle failed");
      }
      finally
      {
        BusinessMetrics.ProviderPollDuration(ProviderMetricLabel, stopwatch.Elapsed.TotalSeconds);

        if (failed)
          BusinessMetrics.ProviderPollFailure(ProviderMetricLabel);
      }

      try
      {
        await Task.Delay(interval, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }
    }
  }

  private async Task PollCandlesAsync(CancellationToken cancellationToken)
  {
    await _lock.WaitAsync(cancellationToken);
    var pairs = _pairs.Count > 0 ? _pairs.ToList() : [];
    _lock.Release();

    BusinessMetrics.TrackedInstrumentsCount(ProviderMetricLabel, pairs.Count);

    if (pairs.Count == 0)
      return;

    var pushed = 0;
    DateTime newest = default;

    foreach (var pair in pairs)
    {
      try
      {
        var klines = await ThrottleRetryHelper.ExecuteAsync(
          c => _api.ExecuteAsync((client, ct) => client.GetKlinesAsync(pair, "1m", 2, null, null, ct), c),
          _provider.ApiLimiter, Logger, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        foreach (var kline in klines)
        {
          var candle = kline.ToCandle(pair, TimeFrame.Minute, BinanceMappers.ExchangeName, nowUtc);
          await _candleBuffer.PushAsync(candle, cancellationToken);
          pushed++;

          if (candle.Timestamp > newest)
            newest = candle.Timestamp;
        }
      }
      catch (ApiException e) when (e.StatusCode == (HttpStatusCode)418)
      {
        // A persistent 418 (IP ban) would waste retries on every remaining pair.
        throw;
      }
      catch (ApiException e) when (e.StatusCode == HttpStatusCode.BadRequest)
      {
        // The pair stopped existing (delisted/renamed): stop polling it.
        Logger.Warning("Binance rejected pair {Pair} ({Reason}), untracking", pair, e.Message);
        await UntrackAsync(new InstrumentKey(pair, BinanceMappers.ExchangeName), cancellationToken);
      }
    }

    if (pushed > 0)
      BusinessMetrics.CandleReceived(ProviderMetricLabel, newest, pushed);

    Logger.Debug("Binance polled {Pairs} pairs, pushed {Pushed} minute candles", pairs.Count, pushed);
  }
}
