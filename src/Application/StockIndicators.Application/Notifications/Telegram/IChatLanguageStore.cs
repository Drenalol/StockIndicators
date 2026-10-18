namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Language of a chat that is not linked to an account yet. The bot has no user row to
/// keep it in, so /language stores the choice per chat id until the chat links, when
/// <c>User.Language</c> takes over and the entry is dropped.
/// </summary>
public interface IChatLanguageStore
{
  Task<string?> GetLanguageAsync(long chatId, CancellationToken cancellationToken = default);

  Task SetLanguageAsync(long chatId, string language, CancellationToken cancellationToken = default);

  Task ClearAsync(long chatId, CancellationToken cancellationToken = default);
}
