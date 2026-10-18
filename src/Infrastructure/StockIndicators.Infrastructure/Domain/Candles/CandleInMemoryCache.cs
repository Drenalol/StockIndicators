using System.Collections.Immutable;
using LazyCache;
using Serilog;
using StockIndicators.Application.Rules;
using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles.Accumulators;

namespace StockIndicators.Infrastructure.Domain.Candles;

/// <summary>
/// L1 (in-memory) layer of the candle cache, backed by L2 (Redis) via <see cref="ICandleStore"/>.
/// On an L1 miss the history is hydrated from L2 so a restarted API keeps the pre-restart candles.
/// </summary>
public class CandleInMemoryCache : ICandleCache
{
  private readonly IAppCache _cache;
  private readonly ICandleStore _store;
  private readonly ILogger _logger;
  private readonly ITradingSessionGate? _tradingSessionGate;
  private readonly SemaphoreSlim _lock = new(1, 1);

  private static readonly IEnumerable<TimeFrame> TimeFramesWithoutMinute = [TimeFrame.Hour, TimeFrame.Day, TimeFrame.Week, TimeFrame.Month];

  /// <summary>
  /// How long candle history is kept in the cache. Long enough to cover the weekend,
  /// so trigger charts stay available until the next trading session.
  /// </summary>
  private static readonly TimeSpan CandleCacheTtl = TimeSpan.FromDays(5);

  public CandleInMemoryCache(IAppCache cache, ICandleStore store, ILogger logger, ITradingSessionGate? tradingSessionGate = null)
  {
    _cache = cache;
    _store = store;
    _logger = logger;
    _tradingSessionGate = tradingSessionGate;
  }

  public async Task AddRangeAsync(IEnumerable<Candle> candles, bool isRealTime, CancellationToken cancellationToken = default)
  {
    try
    {
      await _lock.WaitAsync(cancellationToken);

      foreach (var grouping in candles.GroupBy(candle => (candle.Ticker, candle.ExchangeName, candle.TimeFrame)))
      {
        var (ticker, exchangeName, timeFrame) = grouping.Key;
        var key = BuildKey(ticker, exchangeName, timeFrame);

        var cachedCandles = _cache.TryGetValue(key, out Dictionary<DateTime, Candle> existing)
          ? existing
          : await LoadFromStoreAsync(key, timeFrame, cancellationToken) ?? [];

        AddOrReplaceRange(cachedCandles, grouping, timeFrame);
        _cache.Add(key, cachedCandles, DateTimeOffset.UtcNow.Add(CandleCacheTtl));

        try
        {
          await _store.SaveCandlesAsync(key, grouping, CandleCacheTtl, cancellationToken);
        }
        catch (Exception e)
        {
          _logger.Warning(e, "Failed to persist candles to Redis for {Key}", key);
        }

        // Extended-hours minutes join their own day's regular bars only at the exchange's
        // own aggregation rules (Alpaca folds them into Hour/Day bars in history); folding a
        // pre-market minute into the last stored bar here would move yesterday's daily close
        // to a pre-market price, so real-time minutes are folded only inside the regular session.
        if (timeFrame == TimeFrame.Minute && isRealTime && !IsExtendedRealtimeMinute(exchangeName, grouping.Single()))
          AddOrUpdateOtherTimeFrames(ticker, exchangeName, grouping.Single());
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  private static Dictionary<DateTime, Candle> AddOrReplaceRange(Dictionary<DateTime, Candle> cachedCandles, IEnumerable<Candle> candles, TimeFrame timeFrame)
  {
    foreach (var candle in candles)
    {
      var key = candle.Timestamp.ToTimeFrameDateTime(timeFrame);

      if (cachedCandles.ContainsKey(key))
        cachedCandles.Remove(key);

      cachedCandles.Add(key, candle);
    }

    return cachedCandles;
  }

  private void AddOrUpdateOtherTimeFrames(string ticker, string? exchangeName, Candle minuteCandle)
  {
    foreach (var timeFrame in TimeFramesWithoutMinute)
    {
      if (!_cache.TryGetValue(BuildKey(ticker, exchangeName, timeFrame), out Dictionary<DateTime, Candle> cachedCandles))
        continue;

      var notCompletedCandle = cachedCandles
        .OrderByDescending(kv => kv.Key)
        .FirstOrDefault(kv => IsNotEmpty(kv) && CoversOwnPeriod(kv.Value, minuteCandle.Timestamp, timeFrame));

      if (notCompletedCandle.Key == default)
        continue;

      _logger.Debug("Candle before {@Candle}", notCompletedCandle.Value);
      notCompletedCandle.Value.Update(minuteCandle);
      _logger.Debug("Candle after {@Candle}", notCompletedCandle.Value);
    }

    bool IsNotEmpty(KeyValuePair<DateTime, Candle> kv) => kv.Value.IsNotEmpty && DateTime.UtcNow.AddSeconds(-50) >= kv.Value.Updated;
  }

  /// <summary>
  /// A live minute candle may only fold into the candle of its own period: the candle anchor
  /// sits at or before the minute, but before a full period after it. The current period
  /// candle is often absent from the cache (the providers ship aggregates only after the
  /// period closes, the marker for the missing slot is not a fold target): folding into the
  /// newest older candle instead would silently rewrite history, moving a completed
  /// day/hour/week/month close and volume onto live-session values. The official bar of
  /// the period arrives later through the cache's own marker refresh.
  /// </summary>
  private static bool CoversOwnPeriod(Candle candle, DateTime minuteUtc, TimeFrame timeFrame)
  {
    var age = minuteUtc - DateTime.SpecifyKind(candle.Timestamp, DateTimeKind.Utc);

    return age >= TimeSpan.Zero && age < timeFrame.GetCandlePeriod();
  }

  /// <summary>
  /// True when the gate knows this real-time minute candle belongs to a pre/post-market
  /// window (exchanges without configured extended windows and unknown exchanges never match).
  /// </summary>
  private bool IsExtendedRealtimeMinute(string? exchangeName, Candle minuteCandle) =>
    _tradingSessionGate?.IsExtendedSession(exchangeName, TimeFrame.Minute, minuteCandle.Timestamp) ?? false;

  public async Task<IEnumerable<CandleResult>> GetLastAsync(string ticker, string? exchangeName, TimeFrame timeFrame, CancellationToken cancellationToken = default)
  {
    var key = BuildKey(ticker, exchangeName, timeFrame);

    Dictionary<DateTime, Candle>? cachedCandles = null;

    if (_cache.TryGetValue(key, out Dictionary<DateTime, Candle> existing))
      cachedCandles = existing;
    else if (await LoadFromStoreAsync(key, timeFrame, cancellationToken) is { } hydrated)
    {
      cachedCandles = hydrated;
      _cache.Add(key, hydrated, DateTimeOffset.UtcNow.Add(CandleCacheTtl));
    }

    IReadOnlyDictionary<DateTime, Candle> candles = (IReadOnlyDictionary<DateTime, Candle>?)cachedCandles ?? ImmutableDictionary<DateTime, Candle>.Empty;
    var accumulator = DateTimeAccumulator.Create(timeFrame);

    return accumulator.ForEach(
      current =>
      {
        var candle = candles.TryGetValue(current, out var cachedCandle) ? cachedCandle : null;
        _logger.Verbose("{Ticker} {DateTime} Candle exists in cache: {Exists}", ticker, current, candle != null);
        return new CandleResult(current, candle);
      }
    );
  }

  /// <summary>
  /// Loads the candle history from L2 (Redis) and re-keys it by the time-frame-normalized date,
  /// the way the in-memory dictionary is keyed. Returns null when L2 has nothing (or is unavailable).
  /// </summary>
  private async Task<Dictionary<DateTime, Candle>?> LoadFromStoreAsync(string key, TimeFrame timeFrame, CancellationToken cancellationToken)
  {
    Dictionary<DateTime, Candle>? storedCandles;

    try
    {
      storedCandles = await _store.LoadAsync(key, cancellationToken);
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Failed to load candles from Redis for {Key}", key);
      return null;
    }

    if (storedCandles is not { Count: > 0 })
      return null;

    var cachedCandles = new Dictionary<DateTime, Candle>(storedCandles.Count);

    foreach (var (date, candle) in storedCandles)
      cachedCandles[date.ToTimeFrameDateTime(timeFrame)] = candle;

    _logger.Debug("Loaded {Count} candles from Redis for {Key}", cachedCandles.Count, key);

    return cachedCandles;
  }

  private static string BuildKey(string ticker, string? exchangeName, TimeFrame timeFrame) =>
    string.Join(':', ticker.ToLower(), exchangeName?.ToLower() ?? "*", timeFrame.ToString().ToLower(), "candles");
}
