using System.Runtime.CompilerServices;
using System.Text.Json;
using LazyCache;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Components.Utils;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Binance.Internal;
using StockIndicators.Providers.Binance.Mappers;
using StockIndicators.Providers.Base;
using TimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.Binance;

public class BinanceProviderClient : ProviderClientBase, IInstrumentSource
{
  private const string InstrumentsCacheKey = "binance:instruments";
  private const int KlinesPageLimit = 1000;

  /// <summary>Hard bound for a single instrument lookup on a request path.</summary>
  internal static readonly TimeSpan InstrumentQueryTimeout = TimeSpan.FromSeconds(5);

  private static readonly HashSet<string> QuoteAssets =
  [
    "USDT", "USDC", "USD", "BTC", "ETH"
  ];

  private readonly BinanceProvider _provider;
  private readonly BinanceApiSelector _api;

  public BinanceProviderClient(BinanceProvider provider, BinanceApiSelector api, IAppCache cache, ILogger logger) : base(cache, logger)
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
        var exchangeInfo = await ThrottleRetryHelper.ExecuteAsync(
          c => _api.ExecuteAsync((client, ct) => client.GetExchangeInfoAsync(ct), c),
          _provider.ApiLimiter, Logger, cancellationToken);

        var instruments = exchangeInfo.Symbols
          .Where(symbol => symbol.Status == "TRADING" && symbol.IsSpotTradingAllowed && QuoteAssets.Contains(symbol.QuoteAsset))
          .Select(symbol => (IInstrument)symbol.ToBinanceInstrument())
          .ToHashSet(InstrumentComparer.Instance);

        Logger.Information("Binance instruments loaded: {Count}", instruments.Count);
        return instruments;
      },
      DateTimeOffset.UtcNow.AddDays(1)
    );
  }

  public async Task<IInstrument> GetInstrumentAsync(string ticker, CancellationToken cancellationToken = default)
  {
    // Fast path: a warm instruments cache answers without any provider wait; the hard
    // timeout then only bounds a cold load so a request path never hangs behind Binance.
    if (Cache.TryGetValue<HashSet<IInstrument>>(InstrumentsCacheKey, out var cached) && cached != null)
      return Find(cached) ?? throw new InvalidOperationException($"Symbol {ticker} not found on Binance");

    var instruments = await TimeoutAfter.Run(c => GetInstrumentsAsync(c), InstrumentQueryTimeout, cancellationToken);
    return Find(instruments) ?? throw new InvalidOperationException($"Symbol {ticker} not found on Binance");

    IInstrument? Find(HashSet<IInstrument> instruments) =>
      instruments.FirstOrDefault(candidate => string.Equals(candidate.Ticker, ticker, StringComparison.OrdinalIgnoreCase));
  }

  public async IAsyncEnumerable<Candle> GetHistoryAsync(
    string ticker,
    DateTime from,
    DateTime to,
    TimeFrame timeFrame,
    [EnumeratorCancellation] CancellationToken cancellationToken = default
  )
  {
    var interval = timeFrame.ToKlineInterval();
    var maxCandles = timeFrame.GetMaxPeriod();
    var startTime = new DateTimeOffset(DateTime.SpecifyKind(from, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds();
    var endTime = new DateTimeOffset(DateTime.SpecifyKind(to, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds();
    var collected = 0;

    while (collected < maxCandles)
    {
      var limit = Math.Min(KlinesPageLimit, maxCandles - collected);

      var page = await ThrottleRetryHelper.ExecuteAsync(
        c => _api.ExecuteAsync((client, ct) => client.GetKlinesAsync(ticker, interval, limit, startTime, endTime, ct), c),
        _provider.ApiLimiter, Logger, cancellationToken);

      if (page.Count == 0)
        yield break;

      Logger.Debug("Binance history: {Ticker} {TimeFrame} {Start:O}-{End:O} page={Count}", ticker, timeFrame, from, to, page.Count);

      var nowUtc = DateTime.UtcNow;
      foreach (var kline in page)
        yield return kline.ToCandle(ticker, timeFrame, BinanceMappers.ExchangeName, nowUtc);

      collected += page.Count;
      if (page.Count < limit)
        yield break;

      startTime = page[^1][0].GetInt64() + 1;
    }
  }
}
