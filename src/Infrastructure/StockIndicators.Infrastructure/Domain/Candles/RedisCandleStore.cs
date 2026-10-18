using Serilog;
using StackExchange.Redis;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles;

/// <summary>
/// Redis (L2) candle storage: one HASH per cache key, field = candle date ticks, value = JSON candle.
/// Best-effort: any failure is logged and swallowed so the in-memory layer (L1) keeps working.
/// </summary>
public class RedisCandleStore : ICandleStore
{
  private readonly IConnectionMultiplexer _connection;
  private readonly ISerializer _serializer;
  private readonly ILogger _logger;

  public RedisCandleStore(IConnectionMultiplexer connection, ISerializer serializer, ILogger logger)
  {
    _connection = connection;
    _serializer = serializer;
    _logger = logger;
  }

  public async Task<Dictionary<DateTime, Candle>?> LoadAsync(string key, CancellationToken cancellationToken = default)
  {
    try
    {
      if (!_connection.IsConnected)
      {
        _logger.Debug("Redis is not connected, skipping candle load for {Key}", key);
        return null;
      }

      var entries = await _connection.GetDatabase().HashGetAllAsync(key);

      var candles = new Dictionary<DateTime, Candle>(entries.Length);

      foreach (var entry in entries)
      {
        if (!long.TryParse((string?)entry.Name, out var ticks))
          continue;

        if ((string?)entry.Value is not { } value)
          continue;

        var candle = _serializer.DeserializeOrNull<Candle>(value);

        if (candle == null)
          continue;

        candles[new DateTime(ticks)] = candle;
      }

      return candles.Count > 0 ? candles : null;
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Failed to load candles from Redis for {Key}", key);
      return null;
    }
  }

  public async Task SaveCandlesAsync(string key, IEnumerable<Candle> candles, TimeSpan ttl, CancellationToken cancellationToken = default)
  {
    try
    {
      if (!_connection.IsConnected)
      {
        _logger.Debug("Redis is not connected, skipping candle save for {Key}", key);
        return;
      }

      var transaction = _connection.GetDatabase().CreateTransaction();

      // Transaction operations must not be awaited before ExecuteAsync: the calls only
      // queue the commands, ExecuteAsync is what sends them and completes the queued tasks.
      foreach (var candle in candles)
        _ = transaction.HashSetAsync(key, candle.Timestamp.Ticks, (RedisValue)_serializer.Serialize(candle));

      _ = transaction.KeyExpireAsync(key, ttl);

      await transaction.ExecuteAsync();
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Failed to save candles to Redis for {Key}", key);
    }
  }
}
