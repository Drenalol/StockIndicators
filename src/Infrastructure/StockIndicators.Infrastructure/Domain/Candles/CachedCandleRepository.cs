using Serilog;
using StockIndicators.Abstract;
using StockIndicators.Components.Utils;
using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles.Accumulators;

namespace StockIndicators.Infrastructure.Domain.Candles;

public class CachedCandleRepository : ICandleRepository
{
  private readonly ICandleCache _candleCache;
  private readonly IEnumerable<IInstrumentSource> _instrumentSources;
  private readonly ILogger _logger;

  /// <summary>Hard cap per instrument source so a stalled/unreachable provider cannot hang the whole fetch.</summary>
  private static readonly TimeSpan PerSourceTimeout = TimeSpan.FromSeconds(5);

  public CachedCandleRepository(ICandleCache candleCache, IEnumerable<IInstrumentSource> instrumentSources, ILogger logger)
  {
    _candleCache = candleCache;
    _instrumentSources = instrumentSources;
    _logger = logger;
  }

  public Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default) => _candleCache.AddRangeAsync(candles, true, cancellationToken);

  public async Task<Candle[]> GetLastCandlesAsync(
    string ticker,
    string? exchangeName,
    int periods,
    TimeFrame timeFrame,
    CancellationToken cancellationToken
  )
  {
    return (await GetCandlesAsync(ticker, exchangeName, timeFrame, SortedAndNonEmptyCandles, cancellationToken)).ToArray();

    IEnumerable<Candle> SortedAndNonEmptyCandles(Dictionary<DateTime, Candle?> candles)
    {
      return Get().OrderBy(candle => candle.Timestamp);

      IEnumerable<Candle> Get()
      {
        var count = 0;

        foreach (var (_, value) in candles.OrderByDescending(kv => kv.Key))
        {
          if (value is not { IsNotEmpty: true })
            continue;

          yield return value;

          if (++count == periods)
            yield break;
        }
      }
    }
  }

  public async Task<Candle[]> GetCandlesAsync(
    string ticker,
    string? exchangeName,
    DateTime start,
    DateTime end,
    TimeFrame timeFrame,
    CancellationToken cancellationToken = default
  )
  {
    return (await GetCandlesAsync(ticker, exchangeName, timeFrame, SortedAndNonEmptyCandles, cancellationToken)).ToArray();

    IEnumerable<Candle> SortedAndNonEmptyCandles(Dictionary<DateTime, Candle?> candles)
    {
      foreach (var (key, value) in candles.OrderBy(kv => kv.Key))
        if (value is { IsNotEmpty: true } && key >= start && key <= end)
          yield return value;
    }
  }

  private delegate IEnumerable<Candle> CandleSelector(Dictionary<DateTime, Candle?> candles);

  private async Task<IEnumerable<Candle>> GetCandlesAsync(
    string ticker,
    string? exchangeName,
    TimeFrame timeFrame,
    CandleSelector resultSelector,
    CancellationToken cancellationToken
  )
  {
    var candles = (await _candleCache
        .GetLastAsync(ticker, exchangeName, timeFrame, cancellationToken))
      .ToDictionary(result => result.DateTime, result => result.Candle);

    if (!HasMissingData())
      return resultSelector(candles);

    var toCache = new List<Candle>();

    var missingSlots = candles
      .Where(kv => NeedsSourceData(kv, timeFrame))
      .OrderBy(kv => kv.Key)
      .ToArray();

    var minDt = missingSlots[0].Key;
    var maxDt = missingSlots[^1].Key;

    // A sole missing slot with no marker at all is the newest candle of the current date:
    // the live pipeline delivers its forming candle, so probing it per chart request would
    // only buy an empty provider round-trip
    if (minDt == maxDt && missingSlots[0].Value is null)
      return resultSelector(candles);

    var sourceCandles = await GetCandlesFromSourceAsync(ticker, exchangeName, minDt, maxDt, timeFrame, cancellationToken);

    var sourceCandlesByDateTime = sourceCandles.ToLookup(candle => candle.Timestamp.ToTimeFrameDateTime(timeFrame));

    foreach (var (dateTime, current) in missingSlots)
    {
      var candle = sourceCandlesByDateTime[dateTime].LastOrDefault();

      if (candle is null && current is null && sourceCandles.IsEmpty())
        // The provider has nothing for the date and no marker was ever cached: leave a
        // never-probed slot unmarked (the next-day backfill pulls the official bar). An
        // existing marker is re-marked below so the re-probe stays throttled.
        continue;

      if (candle is null && current is null && IsInsideDelayJitter(dateTime, timeFrame))
        // The provider's "15-minute delay" is approximate: a slot just past the history
        // horizon can still be unpublished while the response carries its neighbours.
        // Unlike Day markers, Minute/Hour empties are never re-probed, so stamping such a
        // gap turns a publication hiccup into a permanent hole; fresh gaps stay unmarked
        // and the next request's probe picks them up when they appear.
        continue;

      candle ??= Candle.Empty(ticker, dateTime, timeFrame, exchangeName);

      candles[dateTime] = candle;
      toCache.Add(candle);
    }

    if (toCache.IsNotEmpty())
      await _candleCache.AddRangeAsync(toCache, false, cancellationToken);

    if (sourceCandles.IsEmpty())
      _logger.Debug("{Ticker} history {TimeFrame} from sources empty for {Start} - {End}", ticker, timeFrame.ToString(), minDt, maxDt);

    return resultSelector(candles);

    bool HasMissingData() => candles.Any(kv => NeedsSourceData(kv, timeFrame));

    bool NeedsSourceData(KeyValuePair<DateTime, Candle?> kv, TimeFrame tf) => kv.Value is null || IsRecheckableMarker(kv, tf);
  }

  /// <summary>
  /// How recent a day-precision empty marker must be to still suspect the provider simply
  /// had not materialized its bar yet: covers the morning-before-the-session probe pattern
  /// plus a long weekend. Older markers are trusted (real holidays and closed days).
  /// </summary>
  private static readonly TimeSpan MarkerRefreshWindow = TimeSpan.FromDays(4);

  /// <summary>Minimum spacing between provider probes of one marker (each probe re-marks it).</summary>
  private static readonly TimeSpan MarkerRecheckDelay = TimeSpan.FromMinutes(15);

  /// <summary>
  /// How recently a Minute/Hour gap must be to still suspect the provider simply has not
  /// published it yet: generously above the free-tier delay horizon, because a missing slot
  /// inside this window must not be stamped into a permanent hole (see the probe loop).
  /// </summary>
  private static readonly TimeSpan DelayJitterWindow = TimeSpan.FromHours(2);

  private static bool IsInsideDelayJitter(DateTime slotStart, TimeFrame timeFrame) =>
    timeFrame is TimeFrame.Minute or TimeFrame.Hour &&
    DateTime.UtcNow - slotStart < DelayJitterWindow;

  /// <summary>
  /// "No candles exist for this date" markers are cached to keep every chart request from
  /// re-querying the provider (weekends, holidays). On day-precision series a marker must
  /// never become a permanent hole: the provider materializes a day's aggregate only when
  /// that day's session is (nearly) over, so a slot probed earlier in the day comes back
  /// empty and would otherwise stay empty forever, losing the day from the daily chart.
  /// Such recent markers are re-probed, with <see cref="MarkerRecheckDelay" /> pacing.
  /// </summary>
  private static bool IsRecheckableMarker(KeyValuePair<DateTime, Candle?> kv, TimeFrame timeFrame) =>
    kv.Value is { IsNotEmpty: false, Updated: var updated } &&
    timeFrame is TimeFrame.Day or TimeFrame.Week or TimeFrame.Month &&
    DateTime.UtcNow - kv.Key < MarkerRefreshWindow &&
    DateTime.UtcNow - updated >= MarkerRecheckDelay;

  private async Task<Candle[]> GetCandlesFromSourceAsync(
    string ticker,
    string? exchangeName,
    DateTime start,
    DateTime end,
    TimeFrame timeFrame,
    CancellationToken cancellationToken
  )
  {
    foreach (var instrumentSource in _instrumentSources)
    {
      if (cancellationToken.IsCancellationRequested)
        return [];

      using var sourceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      sourceCts.CancelAfter(PerSourceTimeout);

      var sourceTask = GetSourceCandlesAsync(instrumentSource, ticker, exchangeName, start, end, timeFrame, sourceCts.Token);

      // A per-source call may include awaits that cannot honor cancellation
      // (e.g. a shared cache-factory task seeded by another caller), so the
      // timeout is enforced here rather than relying on the token alone.
      if (await Task.WhenAny(sourceTask, Task.Delay(PerSourceTimeout, cancellationToken)) != sourceTask)
      {
        sourceCts.Cancel();
        _logger.Warning(
          "Instrument source timed out after {TimeoutSeconds}s for {Ticker} ({ExchangeName}) {TimeFrame}",
          PerSourceTimeout.TotalSeconds,
          ticker,
          exchangeName,
          timeFrame
        );
        continue;
      }

      var history = await sourceTask;

      if (history.IsNotEmpty())
        return history;
    }

    _logger.Warning("{TimeFrame} candles for {Ticker} not found from sources by period {Start} - {End}", timeFrame.ToString(), ticker, start, end);
    return [];
  }

  private async Task<Candle[]> GetSourceCandlesAsync(
    IInstrumentSource instrumentSource,
    string ticker,
    string? exchangeName,
    DateTime start,
    DateTime end,
    TimeFrame timeFrame,
    CancellationToken cancellationToken
  )
  {
    try
    {
      // Strict source scoping: a subscription (or rule) bound to an exchange must
      // only be fed by the source that actually owns the instrument on that exchange
      if (!await SourceOwnsInstrumentAsync(instrumentSource, ticker, exchangeName, cancellationToken))
        return [];

      return await instrumentSource
        .GetHistoryAsync(ticker, start, end, timeFrame, cancellationToken)
        .ToArrayAsync(cancellationToken);
    }
    catch (Exception e)
    {
      // An empty sequence lets the next source take over, but a source that is permanently
      // broken must not be indistinguishable from one that simply has no data: leave the
      // reason in the trace, otherwise "not found from sources" is all production ever sees
      _logger.Debug(e, "{Ticker} ({ExchangeName}) history probe of {Source} failed", ticker, exchangeName, instrumentSource.GetType().Name);
      return [];
    }
  }

  private async Task<bool> SourceOwnsInstrumentAsync(
    IInstrumentSource source,
    string ticker,
    string? exchangeName,
    CancellationToken cancellationToken
  )
  {
    try
    {
      var instrument = await source.GetInstrumentAsync(ticker, cancellationToken);
      return exchangeName == null || string.Equals(instrument.ExchangeName, exchangeName, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }
}
