using System.Net;
using System.Runtime.CompilerServices;
using LazyCache;
using Refit;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Components.Utils;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.BusinessQuant.Api;
using StockIndicators.Providers.BusinessQuant.Mappers;
using StockIndicators.Providers.BusinessQuant.Schema;
using StockIndicators.Providers.Base;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.BusinessQuant;

public class BusinessQuantProviderClient : ProviderClientBase, IInstrumentSource
{
  private const string InstrumentsCacheKey = "businessquant:instruments";
  private const string InstrumentCacheKeyPrefix = "businessquant:instrument:";
  private const string TradableSecurityTypes = "Equity,ETF";

  /// <summary>Hard bound for a single instrument lookup on a request path.</summary>
  internal static readonly TimeSpan InstrumentQueryTimeout = TimeSpan.FromSeconds(5);

  private readonly BusinessQuantProvider _provider;
  private readonly IBusinessQuantApi _api;

  public BusinessQuantProviderClient(BusinessQuantProvider provider, IBusinessQuantApi api, IAppCache cache, ILogger logger) : base(cache, logger)
  {
    _provider = provider;
    _api = api;
  }

  public async Task<HashSet<IInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default)
  {
    return await Cache.GetOrAddAsync(
      InstrumentsCacheKey,
      async () =>
      {
        var response = await ThrottleRetryHelper.ExecuteAsync(
          c => _api.GetUniverseAsync(_provider.ApiKey, TradableSecurityTypes, c),
          _provider.InstrumentsLimiter, Logger, cancellationToken);

        var instruments = (response.Data ?? [])
          .Select(record => (IInstrument)record.ToBusinessQuantInstrument())
          .ToHashSet(InstrumentComparer.Instance);

        Logger.Information("BusinessQuant instruments loaded: {Count}", instruments.Count);
        return instruments;
      },
      DateTimeOffset.UtcNow.AddDays(1)
    );
  }

  public async Task<IInstrument> GetInstrumentAsync(string ticker, CancellationToken cancellationToken = default)
  {
    var cacheKey = InstrumentCacheKeyPrefix + ticker.ToUpperInvariant();
    if (await Cache.GetAsync<BusinessQuantInstrument>(cacheKey) is { } cached)
      return cached;

    // Fast path: a warm instruments cache answers without any provider wait; the hard
    // timeout then only bounds a cold load so a request path never hangs behind BusinessQuant.
    var instruments = await TimeoutAfter.Run(c => GetInstrumentsAsync(c), InstrumentQueryTimeout, cancellationToken);
    var instrument = instruments
      .FirstOrDefault(candidate => string.Equals(candidate.Ticker, ticker, StringComparison.OrdinalIgnoreCase))
      ?? throw new InvalidOperationException($"Ticker {ticker} not found in BusinessQuant universe");

    Cache.Add(cacheKey, instrument, DateTimeOffset.UtcNow.AddDays(1));
    return instrument;
  }

  public async IAsyncEnumerable<Candle> GetHistoryAsync(
    string ticker,
    DateTime from,
    DateTime to,
    TimeFrame timeFrame,
    [EnumeratorCancellation] CancellationToken cancellationToken = default
  )
  {
    // Hour/Week/Month are not supported by the vendor (no aggregation in v1):
    // an empty sequence lets the candle repository fall through to another source.
    if (timeFrame is not (TimeFrame.Minute or TimeFrame.Day))
      yield break;

    var exchangeName = await GetExchangeNameAsync(ticker, cancellationToken);
    var mode = timeFrame.ToQuotesMode();
    var limit = timeFrame.GetMaxPeriod();
    var page = 1;

    while (true)
    {
      BusinessQuantQuotesResponse response;
      try
      {
        response = await ThrottleRetryHelper.ExecuteAsync(
          c => _api.GetQuotesAsync(
            _provider.ApiKey,
            ticker,
            mode,
            null,
            from.ToString("yyyy-MM-dd"),
            to.ToString("yyyy-MM-dd"),
            limit,
            page,
            c),
          _provider.HistoryLimiter, Logger, cancellationToken);
      }
      catch (ApiException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
      {
        // The vendor answers 404 "No data found for the given parameters" for a valid
        // query that matches nothing (e.g. a range without trading sessions):
        // an empty sequence lets the candle repository fall through to another source.
        Logger.Debug("BusinessQuant history for {Ticker} {TimeFrame} {Start:O}-{End:O} has no data (404)", ticker, timeFrame, from, to);
        yield break;
      }

      var bars = response.Data ?? [];
      Logger.Debug(
        "BusinessQuant history: {Ticker} {TimeFrame} {Start:O}-{End:O} page={Page} bars={Count}",
        ticker, timeFrame, from, to, page, bars.Count);

      foreach (var bar in bars.OrderBy(candidate => candidate.Date, StringComparer.Ordinal))
        yield return bar.ToCandle(ticker, timeFrame, exchangeName, true);

      var pagination = response.Metadata?.Pagination;
      if (pagination is null || page >= pagination.TotalPages)
        yield break;

      page++;
    }
  }

  private async Task<string?> GetExchangeNameAsync(string ticker, CancellationToken cancellationToken)
  {
    try
    {
      return (await GetInstrumentAsync(ticker, cancellationToken)).ExchangeName;
    }
    catch (Exception e)
    {
      Logger.Warning("BusinessQuant exchange for {Ticker} not resolved: {Reason}", ticker, e.Message);
      return null;
    }
  }
}
