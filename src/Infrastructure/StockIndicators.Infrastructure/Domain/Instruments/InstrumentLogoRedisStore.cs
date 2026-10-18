using Serilog;
using StackExchange.Redis;
using StockIndicators.Application.Instruments.Logos;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Redis (L2) instrument logo storage: one HASH per instrument (image bytes + metadata
/// fields, or a single negative marker field). Logos are re-resolved from the sources at
/// most once a month, so positives carry a long TTL that outlives the revalidation window;
/// negatives expire fast so a later source addition is picked up within a day.
/// Best-effort like <see cref="Candles.RedisCandleStore"/>: any Redis failure is logged and
/// reported as a cache miss (the cascade then refills, and an unreachable Redis never
/// breaks the logo endpoint).
/// </summary>
public class InstrumentLogoRedisStore : IInstrumentLogoStore
{
  private static readonly TimeSpan PositiveTtl = TimeSpan.FromDays(45);
  private static readonly TimeSpan NegativeTtl = TimeSpan.FromHours(24);

  internal static readonly RedisValue DataField = "data";
  internal static readonly RedisValue ContentTypeField = "ct";
  internal static readonly RedisValue SourceField = "src";
  internal static readonly RedisValue FetchedAtField = "fetched";
  internal static readonly RedisValue NegativeField = "neg";

  private readonly IConnectionMultiplexer _connection;
  private readonly ILogger _logger;

  public InstrumentLogoRedisStore(IConnectionMultiplexer connection, ILogger logger)
  {
    _connection = connection;
    _logger = logger;
  }

  public async Task<InstrumentLogoCacheEntry?> GetAsync(string key, CancellationToken cancellationToken)
  {
    try
    {
      if (!_connection.IsConnected)
        return null;

      var database = _connection.GetDatabase();

      if (!(await database.HashGetAsync(key, NegativeField).ConfigureAwait(false)).IsNull)
        return new InstrumentLogoCacheEntry(null, true);

      var dataValue = await database.HashGetAsync(key, DataField).ConfigureAwait(false);
      var contentTypeValue = await database.HashGetAsync(key, ContentTypeField).ConfigureAwait(false);

      if (dataValue.IsNull || contentTypeValue.IsNull)
        return null;

      var data = (byte[]?)dataValue;
      var contentType = (string?)contentTypeValue;

      if (data is null or { Length: 0 } || contentType is null)
        return null;

      var sourceValue = await database.HashGetAsync(key, SourceField).ConfigureAwait(false);
      var fetchedValue = await database.HashGetAsync(key, FetchedAtField).ConfigureAwait(false);
      var fetchedTicks = long.TryParse((string?)fetchedValue, out var ticks) ? ticks : 0;

      return InstrumentLogoCacheEntry.From(new InstrumentLogoData(data, contentType, (string?)sourceValue ?? "", new DateTime(fetchedTicks, DateTimeKind.Utc)));
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Failed to load the instrument logo from Redis for {Key}", key);
      return null;
    }
  }

  public async Task SaveAsync(string key, InstrumentLogoData logo, CancellationToken cancellationToken)
  {
    try
    {
      if (!_connection.IsConnected)
        return;

      var database = _connection.GetDatabase();

      var transaction = database.CreateTransaction();

      // Queued commands must not be awaited before ExecuteAsync (see RedisCandleStore).
      _ = transaction.HashSetAsync(
        key,
        [
          new HashEntry(DataField, logo.Data),
          new HashEntry(ContentTypeField, logo.ContentType),
          new HashEntry(SourceField, logo.Source),
          new HashEntry(FetchedAtField, logo.FetchedAtUtc.Ticks.ToString())
        ]);

      _ = transaction.KeyExpireAsync(key, PositiveTtl);

      await transaction.ExecuteAsync();
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Failed to save the instrument logo to Redis for {Key}", key);
    }
  }

  public async Task SaveNegativeAsync(string key, CancellationToken cancellationToken)
  {
    try
    {
      if (!_connection.IsConnected)
        return;

      var database = _connection.GetDatabase();

      var transaction = database.CreateTransaction();

      _ = transaction.HashSetAsync(key, [new HashEntry(NegativeField, "1")]);
      _ = transaction.KeyExpireAsync(key, NegativeTtl);

      await transaction.ExecuteAsync();
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Failed to cache the negative instrument logo marker in Redis for {Key}", key);
    }
  }
}
