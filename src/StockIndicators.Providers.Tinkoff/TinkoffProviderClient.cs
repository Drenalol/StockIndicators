using System.Runtime.CompilerServices;
using Google.Protobuf.WellKnownTypes;
using LazyCache;
using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Components.Utils;
using StockIndicators.Application.Instruments.Logos;
using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Tinkoff.Mappers;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;
using Candle = StockIndicators.Domain.Models.Candles.Candle;

namespace StockIndicators.Providers.Tinkoff;

public class TinkoffProviderClient : ProviderClientBase, IInstrumentSource, IInstrumentBrandLogoDirectory
{
  /// <summary>Hard bound for a single instrument lookup on a request path.</summary>
  internal static readonly TimeSpan InstrumentQueryTimeout = TimeSpan.FromSeconds(5);

  private readonly TinkoffProvider _provider;
  private readonly InvestApiClient _client;

  public TinkoffProviderClient(
    TinkoffProvider provider,
    IAppCache cache,
    InvestApiClient client,
    ILogger logger
  ) : base(cache, logger)
  {
    _provider = provider;
    _client = client;
  }

  public async Task<IInstrument> GetInstrumentAsync(string ticker, CancellationToken cancellationToken = default)
  {
    // Fast path: a warm instruments cache answers without any provider wait; the hard
    // timeout then only bounds a cold load so a request path never hangs behind Tinkoff.
    if (Cache.TryGetValue<IDictionary<string, IInstrument>>("tinkoff:instruments", out var cached) && cached != null)
      return Find(cached) ?? throw new KeyNotFoundException($"Instrument {ticker} not found on Tinkoff");

    var instruments = await TimeoutAfter.Run(c => LoadInstrumentsAsync(c), InstrumentQueryTimeout, cancellationToken);
    return Find(instruments) ?? throw new KeyNotFoundException($"Instrument {ticker} not found on Tinkoff");

    IInstrument? Find(IDictionary<string, IInstrument> instruments) =>
      instruments.TryGetValue(ticker, out var instrument)
        ? instrument
        : instruments.Values.FirstOrDefault(candidate => string.Equals(candidate.Ticker, ticker, StringComparison.OrdinalIgnoreCase));
  }

  public async Task<HashSet<IInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default) =>
    (await LoadInstrumentsAsync(cancellationToken))
    .Select(kv => kv.Value)
    .ToHashSet(InstrumentComparer.Instance);

  /// <summary>
  /// Issuer brand logo file name for a ticker (Brand.LogoName on shares/ETFs). Goes through
  /// the same provider instrument cache as the other lookups (warm answer is instant, a cold
  /// cache loads bounded by the standard <see cref="InstrumentQueryTimeout"/>).
  /// </summary>
  public async Task<string?> TryGetBrandLogoNameAsync(string ticker, CancellationToken cancellationToken = default)
  {
    var instruments = await TimeoutAfter.Run(c => LoadInstrumentsAsync(c), InstrumentQueryTimeout, cancellationToken);

    var instrument =
      instruments.TryGetValue(ticker, out var exact) ? exact : instruments.Values.FirstOrDefault(candidate => string.Equals(candidate.Ticker, ticker, StringComparison.OrdinalIgnoreCase));

    return (instrument as TinkoffInstrument)?.BrandLogoName;
  }

  public async IAsyncEnumerable<Candle> GetHistoryAsync(
    string ticker,
    DateTime from,
    DateTime to,
    TimeFrame timeFrame,
    [EnumeratorCancellation] CancellationToken cancellationToken = default
  )
  {
    // Build instrument ID from cached data
    var instruments = await LoadInstrumentsAsync(cancellationToken);
    if (!instruments.TryGetValue(ticker, out var instrument))
    {
      Logger.Warning("Instrument {Ticker} not found in Tinkoff cache", ticker);
      yield break;
    }

    // Tinkoff accepts: figi, instrument_uid, or ticker_class_code
    var instrumentId = instrument.ClassCode != null
      ? $"{instrument.Ticker}_{instrument.ClassCode}"
      : instrument.Ticker;

    var diff = to - from;
    var (total, maxPerPeriod, incrementPeriod) = GetTimeFrameData();
    var max = total / maxPerPeriod - 1;
    var candles = new List<HistoricCandle>();

    for (var i = 0; i <= Math.Ceiling(max); i++)
    {
      var customFrom = incrementPeriod(from, i);
      var customTo = incrementPeriod(to, i - max);

      var request = new GetCandlesRequest
      {
        InstrumentId = instrumentId,
        From = Timestamp.FromDateTime(customFrom),
        To = Timestamp.FromDateTime(customTo),
        Interval = timeFrame.ToCandleInterval()
      };
      
      await _provider.HistoryLimiter.WaitAsync(cancellationToken);
      var history = await _client.MarketData.GetCandlesAsync(request, cancellationToken: cancellationToken);
      candles.AddRange(history.Candles);
      
      Logger.Debug(
        "History candles {Count} for {Ticker} by {TimeFrame} period {Start}-{End}",
        history.Candles.Count,
        ticker,
        timeFrame.ToString(),
        customFrom,
        customTo
      );
    }
    
    foreach (var candle in candles.ReduceCandlesIfNeeded(timeFrame))
      yield return candle.ToDomain(ticker, timeFrame, instrument.ExchangeName);

    (double Total, int MaxPerPeriod, Func<DateTime, double, DateTime> IncrementPeriod) GetTimeFrameData()
    {
      switch (timeFrame)
      {
        case TimeFrame.Minute:
          const int maxMinutes = 1440;
          return (diff.TotalMinutes, maxMinutes, (dt, multiply) => dt.AddMinutes(maxMinutes * multiply));
        case TimeFrame.Hour:
          const int maxHours = 168;
          return (diff.TotalHours, maxHours, (dt, multiply) => dt.AddHours(maxHours * multiply));
        case TimeFrame.Day:
        case TimeFrame.Week:
        case TimeFrame.Month:
          const int maxDays = 365;
          return (diff.TotalDays, maxDays, (dt, multiply) => dt.AddDays((DateTime.IsLeapYear(dt.Year) ? maxDays + 1 : maxDays) * multiply));
        default:
          throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null);
      }
    }
  }

  private async Task<IDictionary<string, IInstrument>> LoadInstrumentsAsync(CancellationToken cancellationToken) =>
    await Cache.GetOrAddAsync(
      "tinkoff:instruments",
      async () =>
      {
        // The instrument type is derived from the class-level RPC that returned it
        // (Shares => Stock, Etfs => Etf); the proto messages carry no type field.
        await _provider.InstrumentsLimiter.WaitAsync(cancellationToken);
        var shares = (await _client.Instruments.SharesAsync(cancellationToken)).Instruments;

        await _provider.InstrumentsLimiter.WaitAsync(cancellationToken);
        var etfs = (await _client.Instruments.EtfsAsync(cancellationToken)).Instruments;

        Logger.Debug("Tinkoff instruments added to cache: {Shares} shares, {Etfs} etfs", shares.Count, etfs.Count);

        var result = new Dictionary<string, IInstrument>();
        foreach (var share in shares)
          result.TryAdd(share.Ticker, share.ToTinkoffInstrument());
        foreach (var etf in etfs)
          result.TryAdd(etf.Ticker, etf.ToTinkoffInstrument());
        return result;
      },
      DateTimeOffset.UtcNow.AddDays(1)
    );
}
