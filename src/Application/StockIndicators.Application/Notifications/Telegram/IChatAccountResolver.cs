using StockIndicators.Domain.Models.Telegram;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// The account behind a Telegram chat: the user and their linked chats
/// (an account has at most one; the list shape stays for the bot screens).
/// </summary>
public sealed record ChatAccount(User User, IReadOnlyList<TelegramChat> Chats);

/// <summary>
/// Resolves the account linked to a Telegram chat. Returns null when the chat
/// is not linked to any account yet.
/// </summary>
public interface IChatAccountResolver
{
  Task<ChatAccount?> GetByChatIdAsync(long chatId, CancellationToken cancellationToken = default);
}

public class ChatAccountResolver : IChatAccountResolver
{
  private readonly ITelegramChatRepository _chatRepository;
  private readonly IUserRepository _userRepository;

  public ChatAccountResolver(
    ITelegramChatRepository chatRepository,
    IUserRepository userRepository
  )
  {
    _chatRepository = chatRepository;
    _userRepository = userRepository;
  }

  public async Task<ChatAccount?> GetByChatIdAsync(long chatId, CancellationToken cancellationToken = default)
  {
    var chat = await _chatRepository.GetByChatIdAsync(chatId, cancellationToken);

    if (chat?.UserId is not { } userId)
      return null;

    var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

    if (user is null)
      return null;

    var chats = await _chatRepository.GetByUserIdAsync(userId, cancellationToken);

    return new ChatAccount(user, [.. chats]);
  }
}
