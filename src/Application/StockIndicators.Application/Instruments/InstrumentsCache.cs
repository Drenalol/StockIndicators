namespace StockIndicators.Application.Instruments;

/// <summary>
/// Shared cache coordinates for the aggregated instruments list.
/// Used by the GetInstruments/GetInstrument handlers and the startup warm-up
/// (InstrumentsWarmupService) — keep the key and TTL in sync in one place.
/// </summary>
public static class InstrumentsCache
{
  public const string AllKey = "instruments:all";

  /// <summary>
  /// Lifetime of the aggregated list. The warm-up refreshes the entry in the
  /// background before it expires (InstrumentsWarmupOptions.RefreshInterval must
  /// be shorter than this value).
  /// </summary>
  public static readonly TimeSpan ListTtl = TimeSpan.FromDays(1);
}
