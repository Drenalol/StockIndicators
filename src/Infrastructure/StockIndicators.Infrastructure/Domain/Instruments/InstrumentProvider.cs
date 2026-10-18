using System.Reactive.Subjects;
using Nito.AsyncEx;
using Serilog;
using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Registry of instruments the candle providers must poll/stream. Membership is contributed
/// by independent sources (active subscriptions, open chart pages): a key joins on the first
/// claim of any source and leaves only when the last one releases it, so a chart view never
/// tears down a subscription's stream and deactivating a subscription keeps the viewers.
/// Keys are matched case-insensitively (the hub and the subscription paths pass the same
/// instrument in different casings); OnAdded/OnRemoved carry the key as it was given.
/// </summary>
public sealed class InstrumentProvider : IInstrumentProvider, IDisposable
{
  private readonly HashSet<string> _instruments = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<InstrumentKey> _keys = [];
  private readonly Dictionary<InstrumentKey, HashSet<InstrumentWatchSource>> _watchSources = [];
  private readonly SemaphoreSlim _lock = new(1, 1);
  private readonly ILogger _logger;

  public InstrumentProvider(ILogger logger) => _logger = logger.ForContext<InstrumentProvider>();

  public IReadOnlySet<string> Instruments => _instruments;
  public IReadOnlyCollection<InstrumentKey> Keys
  {
    get
    {
      using var lockTaken = _lock.Lock();
      return _keys.ToList();
    }
  }

  public Subject<InstrumentKey> OnAdded { get; } = new();
  public Subject<InstrumentKey> OnRemoved { get; } = new();

  /// <summary>
  /// Startup restore (active subscriptions): claims the keys for the subscription source
  /// without replaying add events - the providers pick the membership up from Keys.
  /// </summary>
  public void Init(IEnumerable<InstrumentKey> keys)
  {
    using var lockTaken = _lock.Lock();

    foreach (var key in keys)
      if (TryClaim(key, InstrumentWatchSource.Subscription))
        ApplyAdded(key);
  }

  public void AddWatch(InstrumentKey key, InstrumentWatchSource source)
  {
    var added = false;

    using (_lock.Lock())
      if (TryClaim(key, source))
      {
        ApplyAdded(key);
        added = true;
      }

    if (!added)
      return;

    // Only the real 0→1 transitions: the providers pick the instrument up right here
    _logger.Information(
      "Watch list: {Ticker} ({ExchangeName}) started watching via {Source}",
      key.Ticker, key.ExchangeName ?? "no exchange", source);

    OnAdded.OnNext(key);
  }

  public void RemoveWatch(InstrumentKey key, InstrumentWatchSource source)
  {
    var identity = Normalize(key);

    using (_lock.Lock())
    {
      if (!_watchSources.TryGetValue(identity, out var sources) || !sources.Remove(source))
        return;

      if (sources.Count > 0)
        return;

      _watchSources.Remove(identity);
      _keys.Remove(identity);

      // The ticker stays watched while other exchanges of the same ticker remain
      if (!_keys.Any(watched => watched.Ticker == identity.Ticker))
        _instruments.Remove(identity.Ticker);
    }

    // The last source let go: the providers stop polling/streaming this key right here
    _logger.Information(
      "Watch list: {Ticker} ({ExchangeName}) stopped watching (last source released by {Source})",
      key.Ticker, key.ExchangeName ?? "no exchange", source);

    OnRemoved.OnNext(key);
  }

  public void Dispose()
  {
    OnAdded.Dispose();
    OnRemoved.Dispose();
  }

  /// <summary>Adds the source to the key's claims; true when the key entered the watch list with it.</summary>
  private bool TryClaim(InstrumentKey key, InstrumentWatchSource source)
  {
    var identity = Normalize(key);

    // A released key leaves the dictionary, so an existing entry is always non-empty
    var entered = !_watchSources.TryGetValue(identity, out var sources);

    if (entered)
      _watchSources[identity] = sources = [];

    sources!.Add(source);

    return entered;
  }

  private void ApplyAdded(InstrumentKey key)
  {
    _instruments.Add(key.Ticker);
    _keys.Add(Normalize(key));
  }

  /// <summary>
  /// Case-insensitive instrument identity; an empty exchange normalizes to null
  /// (the same rule as the subscription repository's match keys).
  /// </summary>
  private static InstrumentKey Normalize(InstrumentKey key)
  {
    var exchange = key.ExchangeName?.ToLowerInvariant();

    return new InstrumentKey(key.Ticker.ToLowerInvariant(), string.IsNullOrEmpty(exchange) ? null : exchange);
  }
}
