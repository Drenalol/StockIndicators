using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Telegram;

namespace StockIndicators.Application.Account.LinkTelegramChatByDeepLink;

/// <summary>
/// Raised when a deep-link token is unknown, expired or already consumed.
/// </summary>
public class TelegramLinkTokenInvalidException : Exception
{
  public TelegramLinkTokenInvalidException() : base("telegram_link_invalid")
  {
  }
}

/// <summary>
/// Raised when the token is valid but the chat is already bound to a different account: the
/// user must unlink it from that account first, not request a new link (a fresh link from this
/// account would hit the same wall). Kept distinct from <see cref="TelegramLinkTokenInvalidException"/>
/// so the bot can say so instead of the misleading "link expired" text.
/// </summary>
public class TelegramChatOwnedByAnotherAccountException : Exception
{
  public TelegramChatOwnedByAnotherAccountException() : base("telegram_chat_owned_by_another_account")
  {
  }
}

public class LinkTelegramChatByDeepLinkCommandHandler : ICommandHandler<LinkTelegramChatByDeepLinkCommand, TelegramChatDto>
{
  private readonly ITelegramLinkTokenStore _linkTokenStore;
  private readonly ITelegramChatRepository _chatRepository;

  public LinkTelegramChatByDeepLinkCommandHandler(
    ITelegramLinkTokenStore linkTokenStore,
    ITelegramChatRepository chatRepository
  )
  {
    _linkTokenStore = linkTokenStore;
    _chatRepository = chatRepository;
  }

  public async Task<TelegramChatDto> Handle(LinkTelegramChatByDeepLinkCommand request, CancellationToken cancellationToken)
  {
    // Possession of the short-lived, single-use token is the authorization — it never enters the chat as free text.
    var userId = await _linkTokenStore.ConsumeAsync(request.Token, cancellationToken)
      ?? throw new TelegramLinkTokenInvalidException();

    var existing = await _chatRepository.GetByChatIdAsync(request.ChatId, cancellationToken);

    // A chat is linked to exactly one user
    if (existing is not null && existing.UserId != Guid.Empty && existing.UserId != userId)
      throw new TelegramChatOwnedByAnotherAccountException();

    // An account has exactly one chat: linking a new one unlinks the previous
    var previous = await _chatRepository.GetByUserIdAsync(userId, cancellationToken);

    foreach (var stale in previous.Where(item => item.ChatId != request.ChatId))
      await _chatRepository.DeleteAsync(stale, cancellationToken);

    var chat = existing
      ?? new TelegramChat { Id = Guid.CreateVersion7(), ChatId = request.ChatId };

    chat.UserId = userId;
    chat.Username = request.Username ?? chat.Username;

    if (existing is null)
      await _chatRepository.AddAsync(chat, cancellationToken);

    await _chatRepository.CommitAsync(cancellationToken);

    return new TelegramChatDto(
      chat.Id.ToString(),
      chat.ChatId,
      chat.Username,
      chat.Created);
  }
}
