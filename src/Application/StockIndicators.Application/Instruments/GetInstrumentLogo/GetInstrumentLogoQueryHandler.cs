using System.Collections.Concurrent;
using LazyCache;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Instruments.Logos;

namespace StockIndicators.Application.Instruments.GetInstrumentLogo;

/// <summary>
/// Read-through resolution of an instrument logo: process-local L1 (short TTL, dedupes
/// concurrent loads), Redis L2 (bytes live there for months, negatives for a day), and the
/// source cascade (Tinkoff brand CDN - Logo.dev - CoinGecko) behind an L2 miss.
/// A stored logo older than <see cref="LogoOptions.RevalidateAfterDays"/> is served as-is and
/// re-resolved from the sources in the background (stale-while-revalidate), so a request
/// never blocks on a monthly refresh. Per-source failures are logged and skipped; when
/// every source misses the negative result is cached too (the UI shows its own monogram).
/// </summary>
public class GetInstrumentLogoQueryHandler : IQueryHandler<GetInstrumentLogoQuery, InstrumentLogoData?>
{
  private static readonly TimeSpan MemoryTtl = TimeSpan.FromMinutes(10);

  /// <summary>Keys with a revalidation in flight in this process (at most one per key).</summary>
  private static readonly ConcurrentDictionary<string, byte> Revalidating = new();

  private readonly IAppCache _cache;
  private readonly IInstrumentLogoStore _store;
  private readonly IInstrumentCacheReader _instrumentCacheReader;
  private readonly List<IInstrumentLogoSource> _sources;
  private readonly LogoOptions _options;
  private readonly ILogger _logger;

  public GetInstrumentLogoQueryHandler(
    IAppCache cache,
    IInstrumentLogoStore store,
    IInstrumentCacheReader instrumentCacheReader,
    IEnumerable<IInstrumentLogoSource> sources,
    IOptions<LogoOptions> options,
    ILogger logger)
  {
    _cache = cache;
    _store = store;
    _instrumentCacheReader = instrumentCacheReader;
    _sources = sources.ToList();
    _options = options.Value;
    _logger = logger;
  }

  public async Task<InstrumentLogoData?> Handle(GetInstrumentLogoQuery request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Ticker))
      return null;

    var key = InstrumentLogoKeys.For(request.Ticker, request.ExchangeName);

    if (_cache.TryGetValue<InstrumentLogoCacheEntry>(key, out var memoryEntry) && memoryEntry != null)
      return FromEntry(key, memoryEntry);

    var entry = await _cache.GetOrAddAsync(
      key,
      () => LoadAsync(key, request, cancellationToken),
      DateTimeOffset.UtcNow.Add(MemoryTtl)
    );

    return FromEntry(key, entry);
  }

  private async Task<InstrumentLogoCacheEntry> LoadAsync(string key, GetInstrumentLogoQuery request, CancellationToken cancellationToken)
  {
    var stored = await _store.GetAsync(key, cancellationToken);

    if (stored != null)
      return stored;

    var catalog = await _instrumentCacheReader.TryGetAsync(request.Ticker, null, cancellationToken);
    var logoRequest = new InstrumentLogoRequest(request.Ticker, request.ExchangeName, catalog);

    foreach (var source in _sources)
    {
      try
      {
        var data = await source.TryResolveAsync(logoRequest, cancellationToken);

        if (data is null)
          continue;

        await _store.SaveAsync(key, data, cancellationToken);
        return InstrumentLogoCacheEntry.From(data);
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Instrument logo source {Source} failed for {Ticker} ({Exchange})", source.GetType().Name, request.Ticker, request.ExchangeName);
      }
    }

    await _store.SaveNegativeAsync(key, cancellationToken);
    return new InstrumentLogoCacheEntry(null, true);
  }

  private InstrumentLogoData? FromEntry(string key, InstrumentLogoCacheEntry entry)
  {
    if (entry.Negative || entry.Logo is not { } logo)
      return null;

    MaybeStartRevalidation(key, logo);

    return logo;
  }

  private void MaybeStartRevalidation(string key, InstrumentLogoData logo)
  {
    var staleAfter = TimeSpan.FromDays(Math.Max(1, _options.RevalidateAfterDays));

    if (DateTime.UtcNow - logo.FetchedAtUtc < staleAfter)
      return;

    if (!Revalidating.TryAdd(key, 0))
      return;

    _ = Task.Run(() => RevalidateAsync(key));
  }

  private async Task RevalidateAsync(string key)
  {
    try
    {
      var (ticker, exchange) = ParseKey(key);

      // A cold catalog cannot confirm the brand file for the ticker: revalidating now would
      // wrongly mark healthy stored logos negative. Skip; the next request retries.
      if (!_instrumentCacheReader.IsWarm)
        return;

      var catalog = await _instrumentCacheReader.TryGetAsync(ticker, null, CancellationToken.None);
      var logoRequest = new InstrumentLogoRequest(ticker, exchange, catalog);

      foreach (var source in _sources)
      {
        try
        {
          var data = await source.TryResolveAsync(logoRequest, CancellationToken.None);

          if (data is null)
            continue;

          await _store.SaveAsync(key, data, CancellationToken.None);
          _cache.Add(key, InstrumentLogoCacheEntry.From(data), DateTimeOffset.UtcNow.Add(MemoryTtl));
          return;
        }
        catch (Exception e)
        {
          _logger.Warning(e, "Instrument logo revalidation failed on {Source} for {Key}", source.GetType().Name, key);
        }
      }

      // Every source lost the logo (e.g. the brand file was pulled): drop the positive cache
      // marker in Redis and L1 but keep serving the stale bytes from the current responses.
      await _store.SaveNegativeAsync(key, CancellationToken.None);
      _cache.Add(key, new InstrumentLogoCacheEntry(null, true), DateTimeOffset.UtcNow.Add(MemoryTtl));
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Instrument logo revalidation failed for {Key}", key);
    }
    finally
    {
      Revalidating.TryRemove(key, out _);
    }
  }

  /// <summary>Recover (ticker, exchange) from an <see cref="InstrumentLogoKeys"/> key ("instrument-logo:sber:binance").</summary>
  public static (string Ticker, string? Exchange) ParseKey(string key)
  {
    var parts = key.Split(':');
    var exchange = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null;
    return (parts.Length > 1 ? parts[1] : key, exchange);
  }
}
