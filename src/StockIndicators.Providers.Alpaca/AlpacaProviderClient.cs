using System.Runtime.CompilerServices;
using Alpaca.Markets;
using LazyCache;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Components.Utils;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Alpaca.Mappers;
using StockIndicators.Providers.Base;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.Alpaca;

public class AlpacaProviderClient : ProviderClientBase, IInstrumentSource
{
  private static readonly AssetsRequest InstrumentsRequest = new()
  {
    AssetClass = AssetClass.UsEquity,
    AssetStatus = AssetStatus.Active
  };

  /// <summary>Hard bound for a single instrument lookup on a request path.</summary>
  internal static readonly TimeSpan InstrumentQueryTimeout = TimeSpan.FromSeconds(5);

  private readonly AlpacaProvider _provider;

  public AlpacaProviderClient(AlpacaProvider provider, IAppCache cache, ILogger logger) : base(cache, logger)
  {
    _provider = provider;
  }

  public async Task<HashSet<IInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default)
  {
    return await Cache.GetOrAddAsync(
      "alpaca:instruments",
      async () =>
      {
        await _provider.InstrumentsLimiter.WaitAsync(cancellationToken);
        var assets = await _provider.TradingClient.ListAssetsAsync(InstrumentsRequest, cancellationToken);
        Logger.Information("Alpaca instruments loaded: {Count}", assets.Count);
        return assets
          .Where(asset => asset.IsTradable)
          .Select(asset => (IInstrument)asset.ToAlpacaInstrument())
          .ToHashSet(InstrumentComparer.Instance);
      },
      DateTimeOffset.UtcNow.AddDays(1)
    );
  }

  public async Task<IInstrument> GetInstrumentAsync(string ticker, CancellationToken cancellationToken = default)
  {
    var cacheKey = $"alpaca:asset:{ticker.ToUpperInvariant()}";
    if (await Cache.GetAsync<AlpacaInstrument>(cacheKey) is { } cached)
      return cached;

    // Hard timeout so a request path never hangs behind an unresponsive Alpaca API.
    // Instruments are reference data: the session-gated limiter would idle the
    // lookup until the next session open when the cache is cold outside trading hours.
    // The symbol is uppercased for the wire: Alpaca keys assets by their (uppercase)
    // symbol, while hub/claim paths pass normalized lowercase keys - a raw lowercase
    // lookup answers 404 and used to silently kill both tracking and history scoping.
    var symbol = ticker.ToUpperInvariant();
    var instrument = await TimeoutAfter.Run(
      async c =>
      {
        await _provider.InstrumentsLimiter.WaitAsync(c);
        return (await _provider.TradingClient.GetAssetAsync(symbol, c)).ToAlpacaInstrument();
      },
      InstrumentQueryTimeout,
      cancellationToken);

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
    // Free tier locks the latest 15 minutes of SIP data, so clamp recent
    // ranges to the 16-minute horizon used by the polling loop.
    var horizon = DateTime.UtcNow.AddMinutes(-16);
    if (to >= horizon)
    {
      if (from >= horizon)
        yield break;
      to = horizon;
    }

    var exchangeName = await GetExchangeNameAsync(ticker, cancellationToken);

    var request = new HistoricalBarsRequest(ticker, from, to, timeFrame.ToBarTimeFrame())
    {
      Adjustment = Adjustment.SplitsOnly,
      Feed = MarketDataFeed.Sip,
      SortDirection = SortDirection.Ascending
    }.WithPageSize(Pagination.MaxPageSize);

    Logger.Debug("Alpaca history: {Ticker} {TimeFrame} {Start:O}-{End:O}", ticker, timeFrame, from, to);

    await _provider.HistoryLimiter.WaitAsync(cancellationToken);
    var page = await _provider.DataClient.GetHistoricalBarsAsync(request, cancellationToken);

    var total = 0;

    while (true)
    {
      foreach (var bar in page.Items.Values.SelectMany(bars => bars))
      {
        yield return bar.ToCandle(timeFrame, exchangeName);
        total++;
      }

      if (page.NextPageToken is null)
        break;

      await _provider.HistoryLimiter.WaitAsync(cancellationToken);
      request = request.WithPageToken(page.NextPageToken);
      page = await _provider.DataClient.GetHistoricalBarsAsync(request, cancellationToken);
    }

    Logger.Debug("Alpaca history: {Ticker} {TimeFrame} {Start:O}-{End:O} bars={Count}", ticker, timeFrame, from, to, total);
  }

  private async Task<string?> GetExchangeNameAsync(string ticker, CancellationToken cancellationToken)
  {
    try
    {
      return (await GetInstrumentAsync(ticker, cancellationToken)).ExchangeName;
    }
    catch (Exception e)
    {
      Logger.Warning("Alpaca exchange for {Ticker} not resolved: {Reason}", ticker, e.Message);
      return null;
    }
  }
}
