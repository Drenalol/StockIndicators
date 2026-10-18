using LazyCache;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.GetInstruments;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Reads the warm aggregated instruments list written by <see cref="InstrumentsWarmupService"/>
/// (LazyCache key <see cref="InstrumentsCache.AllKey"/>). A miss is a plain null — this reader
/// never loads from providers, so a request path can confirm a client-resolved instrument
/// identity without any provider round trip.
/// </summary>
public class InstrumentsCacheReader : IInstrumentCacheReader
{
  private readonly IAppCache _cache;

  public InstrumentsCacheReader(IAppCache cache) => _cache = cache;

  public bool IsWarm => _cache.TryGetValue<List<InstrumentDto>>(InstrumentsCache.AllKey, out _);

  public Task<InstrumentDto?> TryGetAsync(string ticker, string? source, CancellationToken cancellationToken = default)
  {
    // The warm-up stores List<InstrumentDto>; TryGetValue guards against a cold cache.
    if (!_cache.TryGetValue<List<InstrumentDto>>(InstrumentsCache.AllKey, out var instruments) || instruments == null)
      return Task.FromResult<InstrumentDto?>(null);

    var match = instruments.FirstOrDefault(instrument =>
      instrument.Ticker == ticker &&
      (source == null || string.Equals(instrument.Source, source, StringComparison.OrdinalIgnoreCase)));

    return Task.FromResult(match);
  }
}
