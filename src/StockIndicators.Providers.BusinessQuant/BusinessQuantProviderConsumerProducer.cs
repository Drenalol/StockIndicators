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
using StockIndicators.Providers.BusinessQuant.Api;
using StockIndicators.Providers.BusinessQuant.Mappers;
using StockIndicators.Providers.BusinessQuant.Schema;
using StockIndicators.Providers.Base;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.BusinessQuant;

/// <summary>
/// Polls minute bars for tracked US symbols and feeds the shared candle pipeline.
/// A subscription is tracked only when its exchange belongs to the BusinessQuant (US)
/// universe, so MOEX subscriptions are never activated by US candles and vice versa.
/// </summary>
public class BusinessQuantProviderConsumerProducer : ConsumerProviderBase, ICandleSource, IDisposable
{
  private static readonly HashSet<string> UsExchanges = new(StringComparer.OrdinalIgnoreCase)
  {
    "NASDAQ", "NYSE", "OTC"
  };

  private const string PollingMode = "minute-bars";
  private const string PollingPeriod = "1d";
  private const int PollingLimit = 500;

  private readonly BusinessQuantProvider _provider;
  private readonly BusinessQuantProviderClient _client;
  private readonly IBusinessQuantApi _api;
  private readonly BusinessQuantProviderOptions _options;
  private readonly ICandleProvider _candleBuffer;
  private readonly IInstrumentProvider _instrumentProvider;
  private readonly CancellationTokenSource _cts = new();
  private readonly SemaphoreSlim _lock = new(1, 1);
  // Keyed by symbol as returned by the API; removals arrive with the hub/subscription
  // casing, so the dictionary must match symbols case-insensitively
  private readonly Dictionary<string, string> _exchanges = new(StringComparer.OrdinalIgnoreCase);
  private readonly IEnumerable<IDisposable> _subscriptions;

  public BusinessQuantProviderConsumerProducer(
    BusinessQuantProvider provider,
    BusinessQuantProviderClient client,
    IBusinessQuantApi api,
    IOptions<BusinessQuantProviderOptions> options,
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
    // Exchange-aware: only US-bound (or legacy null-exchange) subscriptions are tracked.
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
    if (key.ExchangeName != null && !UsExchanges.Contains(key.ExchangeName))
      return Unit.Default;

    try
    {
      var instrument = await _client.GetInstrumentAsync(key.Ticker, cancellationToken);

      if (instrument.ExchangeName is not { } exchange || !UsExchanges.Contains(exchange))
        return Unit.Default;

      await _lock.WaitAsync(cancellationToken);
      try
      {
        _exchanges[instrument.Ticker] = exchange;
        Logger.Information("BusinessQuant tracking {Ticker} ({Exchange})", instrument.Ticker, exchange);
      }
      finally
      {
        _lock.Release();
      }
    }
    catch (Exception e)
    {
      Logger.Debug("BusinessQuant: {Ticker} not in US universe ({Reason})", key.Ticker, e.Message);
    }

    return Unit.Default;
  }

  private async Task<Unit> UntrackAsync(InstrumentKey key, CancellationToken cancellationToken)
  {
    if (key.ExchangeName != null && !UsExchanges.Contains(key.ExchangeName))
      return Unit.Default;

    await _lock.WaitAsync(cancellationToken);
    try
    {
      if (_exchanges.Remove(key.Ticker))
        Logger.Information("BusinessQuant untracked {Ticker}", key.Ticker);
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
      catch (Exception e)
      {
        failed = true;
        Logger.Warning(e, "BusinessQuant polling cycle failed");
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
    var tracked = _exchanges.Count > 0 ? _exchanges.ToList() : [];
    _lock.Release();

    BusinessMetrics.TrackedInstrumentsCount(ProviderMetricLabel, tracked.Count);

    if (tracked.Count == 0)
      return;

    var quotes = await FetchMinuteBarsAsync(tracked.Select(pair => pair.Key).ToArray(), cancellationToken);
    var pushed = 0;
    DateTime newest = default;

    foreach (var (ticker, bars) in quotes)
    {
      var exchangeName = tracked.ToDictionary(pair => pair.Key, pair => pair.Value).GetValueOrDefault(ticker);

      for (var i = 0; i < bars.Count; i++)
      {
        // The last (newest) bar is still forming: it is pushed as not completed.
        var candle = bars[i].ToCandle(ticker, TimeFrame.Minute, exchangeName, i < bars.Count - 1);
        await _candleBuffer.PushAsync(candle, cancellationToken);
        pushed++;

        if (candle.Timestamp > newest)
          newest = candle.Timestamp;
      }
    }

    if (pushed > 0)
      BusinessMetrics.CandleReceived(ProviderMetricLabel, newest, pushed);

    Logger.Debug("BusinessQuant polled {Tickers} tickers, pushed {Pushed} minute candles", tracked.Count, pushed);
  }

  /// <summary>
  /// One batch call for all tracked tickers (multi-ticker quote). The vendor returns a
  /// plain {metadata, data} block for a single ticker and a dictionary keyed by ticker
  /// for multiple ones, so both shapes are normalized here.
  /// </summary>
  private async Task<IReadOnlyDictionary<string, List<BusinessQuantQuoteBar>>> FetchMinuteBarsAsync(
    IReadOnlyList<string> tickers,
    CancellationToken cancellationToken
  )
  {
    var responses = tickers.Count == 1
      ? await GetSingleQuotesAsync(tickers[0], cancellationToken)
      : await GetBatchQuotesAsync(tickers, cancellationToken);

    return responses
      .Where(kv => kv.Value.Data is { Count: > 0 })
      .ToDictionary(kv => kv.Key, kv => (kv.Value.Data ?? [])
        .OrderBy(bar => BusinessQuantMappers.ToUtcDateTime(bar.Date))
        .ToList());
  }

  private async Task<Dictionary<string, BusinessQuantQuotesResponse>> GetSingleQuotesAsync(string ticker, CancellationToken cancellationToken)
  {
    BusinessQuantQuotesResponse response;
    try
    {
      response = await ThrottleRetryHelper.ExecuteAsync(
        c => _api.GetQuotesAsync(_provider.ApiKey, ticker, PollingMode, PollingPeriod, null, null, PollingLimit, null, c),
        _provider.ApiLimiter, Logger, cancellationToken);
    }
    catch (ApiException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
    {
      // The vendor answers 404 "No data found" when the period has no bars (e.g. market closed).
      Logger.Debug("BusinessQuant polling has no data for {Ticker} (404)", ticker);
      return [];
    }

    return new Dictionary<string, BusinessQuantQuotesResponse> { [ticker] = response };
  }

  private async Task<Dictionary<string, BusinessQuantQuotesResponse>> GetBatchQuotesAsync(IReadOnlyList<string> tickers, CancellationToken cancellationToken)
  {
    try
    {
      return await ThrottleRetryHelper.ExecuteAsync(
        c => _api.GetQuotesBatchAsync(_provider.ApiKey, string.Join(",", tickers), PollingMode, PollingPeriod, PollingLimit, c),
        _provider.ApiLimiter, Logger, cancellationToken);
    }
    catch (ApiException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
    {
      // The vendor answers 404 "No data found" when the period has no bars (e.g. market closed).
      Logger.Debug("BusinessQuant polling has no data for {Tickers} (404)", string.Join(",", tickers));
      return [];
    }
  }
}
