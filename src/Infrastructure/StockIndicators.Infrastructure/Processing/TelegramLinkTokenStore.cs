using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;
using StockIndicators.Application.Notifications.Telegram;

namespace StockIndicators.Infrastructure.Processing;

/// <summary>
/// Stores one-time Telegram chat-link tokens. Redis-backed with an in-memory fallback for this
/// instance when Redis is unavailable.
/// <para>
/// <c>tgvlink:token:&lt;token&gt;</c> → user id (consumed once, GETDEL);
/// <c>tgvlink:user:&lt;userId&gt;</c> → the active token, so a repeated issue reuses it instead of
/// invalidating a deep link the user may already be opening, sliding its TTL to match the fresh
/// expiry handed to the UI. TTL from <see cref="TelegramLinkTokenOptions"/>.
/// </para>
/// </summary>
public class TelegramLinkTokenStore : ITelegramLinkTokenStore
{
  private const string TokenPrefix = "tgvlink:token:";
  private const string UserPrefix = "tgvlink:user:";
  private const int TokenBytes = 24;

  private readonly IConnectionMultiplexer _connection;
  private readonly ILogger _logger;
  private readonly TimeSpan _ttl;

  // Memory fallback (Redis unavailable): token → (user, expiry) and user → active token
  private readonly ConcurrentDictionary<string, MemoryEntry> _memoryTokens = new();
  private readonly ConcurrentDictionary<Guid, MemoryEntry> _memoryUsers = new();

  public TelegramLinkTokenStore(
    IConnectionMultiplexer connection,
    IOptions<TelegramLinkTokenOptions> options,
    ILogger logger
  )
  {
    _connection = connection;
    _logger = logger;
    _ttl = TimeSpan.FromMinutes(Math.Clamp(options.Value.TtlMinutes, 1, 24 * 60));
  }

  public async Task<IssuedTelegramLinkToken> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
  {
    if (_connection.IsConnected)
    {
      try
      {
        return await IssueInRedisAsync(userId);
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Failed to issue Telegram link token for {UserId} in Redis, falling back to memory", userId);
      }
    }

    return IssueInMemory(userId);
  }

  public async Task<Guid?> ConsumeAsync(string token, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(token))
      return null;

    if (_connection.IsConnected)
    {
      try
      {
        return await ConsumeInRedisAsync(token);
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Failed to consume Telegram link token in Redis, falling back to memory");
      }
    }

    return ConsumeInMemory(token);
  }

  private async Task<IssuedTelegramLinkToken> IssueInRedisAsync(Guid userId)
  {
    var db = _connection.GetDatabase();
    var userKey = UserPrefix + userId;
    var existing = (string?)await db.StringGetAsync(userKey);

    // Reuse must slide both TTLs: the returned expiry is what the UI shows the user, and a
    // reused token dying on its old, shorter TTL makes a "still valid" link come back invalid.
    if (!string.IsNullOrEmpty(existing))
    {
      var tokenKey = TokenPrefix + existing;

      if (await db.KeyExpireAsync(tokenKey, _ttl) && await db.KeyExpireAsync(userKey, _ttl))
        return new IssuedTelegramLinkToken(existing, DateTime.UtcNow.Add(_ttl));
    }

    var issued = NewToken();

    await db.StringSetAsync(TokenPrefix + issued.Token, userId.ToString(), _ttl);
    await db.StringSetAsync(userKey, issued.Token, _ttl);

    return issued;
  }

  private async Task<Guid?> ConsumeInRedisAsync(string token)
  {
    var db = _connection.GetDatabase();

    // Atomic get-and-delete: a token presented twice loses the race and returns null
    var stored = (string?)await db.StringGetDeleteAsync(TokenPrefix + token);

    if (string.IsNullOrEmpty(stored) || !Guid.TryParse(stored, out var userId))
      return null;

    await db.KeyDeleteAsync(UserPrefix + userId);

    return userId;
  }

  private IssuedTelegramLinkToken IssueInMemory(Guid userId)
  {
    if (_memoryUsers.TryGetValue(userId, out var existing) && existing.ExpiresAt > DateTime.UtcNow)
    {
      // Same slide as the Redis path: the reused token lives a fresh TTL from now
      var refreshed = new MemoryEntry(existing.Token, userId, DateTime.UtcNow.Add(_ttl));

      _memoryUsers[userId] = refreshed;
      _memoryTokens[refreshed.Token] = refreshed;

      return new IssuedTelegramLinkToken(refreshed.Token, refreshed.ExpiresAt);
    }

    var issued = NewToken();
    var entry = new MemoryEntry(issued.Token, userId, issued.ExpiresAt);

    _memoryUsers[userId] = entry;
    _memoryTokens[issued.Token] = entry;

    return issued;
  }

  private Guid? ConsumeInMemory(string token)
  {
    if (!_memoryTokens.TryRemove(token, out var entry) || entry.ExpiresAt <= DateTime.UtcNow)
      return null;

    _memoryUsers.TryRemove(entry.UserId, out _);

    return entry.UserId;
  }

  private IssuedTelegramLinkToken NewToken() =>
    new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_'), DateTime.UtcNow.Add(_ttl));

  private readonly record struct MemoryEntry(string Token, Guid UserId, DateTime ExpiresAt);
}
