using System.Collections.Concurrent;
using Serilog;
using StackExchange.Redis;
using StockIndicators.Application.Notifications.Telegram;

namespace StockIndicators.Infrastructure.Processing;

/// <summary>
/// The language a chat picked through /language while it was not linked to an account yet
/// (the same degradation to memory as the other stores when Redis is unavailable - losing
/// the choice there only costs a re-pick). Cleared when the chat links, from then on
/// <c>User.Language</c> answers.
/// <para>
/// <c>tglang:chat:&lt;chatId&gt;</c> → language code.
/// </para>
/// </summary>
public class ChatLanguageStore : IChatLanguageStore
{
  private const string ChatPrefix = "tglang:chat:";

  private readonly IConnectionMultiplexer _connection;
  private readonly ILogger _logger;

  // Memory fallback (Redis unavailable): chat id → language
  private readonly ConcurrentDictionary<long, string> _memory = new();

  public ChatLanguageStore(IConnectionMultiplexer connection, ILogger logger)
  {
    _connection = connection;
    _logger = logger.ForContext<ChatLanguageStore>();
  }

  public async Task<string?> GetLanguageAsync(long chatId, CancellationToken cancellationToken = default)
  {
    if (_connection.IsConnected)
    {
      try
      {
        var value = await _connection.GetDatabase().StringGetAsync(ChatPrefix + chatId);

        return value.IsNullOrEmpty ? null : (string?)value;
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Failed to read the language of chat {ChatId} in Redis, falling back to memory", chatId);
      }
    }

    return _memory.TryGetValue(chatId, out var language) ? language : null;
  }

  public async Task SetLanguageAsync(long chatId, string language, CancellationToken cancellationToken = default)
  {
    if (_connection.IsConnected)
    {
      try
      {
        await _connection.GetDatabase().StringSetAsync(ChatPrefix + chatId, language);

        return;
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Failed to store the language of chat {ChatId} in Redis, falling back to memory", chatId);
      }
    }

    _memory[chatId] = language;
  }

  public async Task ClearAsync(long chatId, CancellationToken cancellationToken = default)
  {
    if (_connection.IsConnected)
    {
      try
      {
        await _connection.GetDatabase().KeyDeleteAsync(ChatPrefix + chatId);

        return;
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Failed to drop the language of chat {ChatId} in Redis, falling back to memory", chatId);
      }
    }

    _memory.TryRemove(chatId, out _);
  }
}
