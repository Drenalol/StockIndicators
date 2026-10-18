namespace StockIndicators.Domain.Models.Candles;

/// <summary>
/// Durable (Redis) layer of the candle cache: survives API restarts.
/// All operations are best-effort — failures must not break the in-memory layer.
/// </summary>
public interface ICandleStore
{
  /// <summary>
  /// Loads the full candle history for a cache key, keyed by the raw candle date.
  /// Returns null when nothing is stored or the load failed.
  /// </summary>
  Task<Dictionary<DateTime, Candle>?> LoadAsync(string key, CancellationToken cancellationToken = default);

  /// <summary>
  /// Stores candles for a cache key (per-field upsert) and refreshes the key TTL.
  /// </summary>
  Task SaveCandlesAsync(string key, IEnumerable<Candle> candles, TimeSpan ttl, CancellationToken cancellationToken = default);
}
