using StockIndicators.Application.Instruments.GetInstruments;

namespace StockIndicators.Application.Instruments;

/// <summary>
/// Read access to the warm aggregated instruments list (<see cref="InstrumentsCache.AllKey"/>)
/// without touching providers. Implemented in Infrastructure (over <c>IAppCache</c>);
/// optional on the request path — when the cache is cold the caller falls back to providers.
/// </summary>
public interface IInstrumentCacheReader
{
  /// <summary>
  /// Finds an instrument by ticker in the aggregated list. When <paramref name="source"/> is
  /// given only entries from that provider match (client-supplied identity must not be
  /// confirmed by another provider's entry); null matches any provider. Comparison is
  /// case-insensitive. Returns null on a cache miss or no match.
  /// </summary>
  Task<InstrumentDto?> TryGetAsync(string ticker, string? source, CancellationToken cancellationToken = default);

  /// <summary>
  /// Whether the aggregated list is loaded (warmed). A negative answer is authoritative
  /// only while the warm-up never succeeded; callers use it to distinguish "instrument
  /// absent from the list" from "list not loaded yet" without blocking on providers.
  /// </summary>
  bool IsWarm { get; }
}
