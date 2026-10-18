using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Contracts.Auth;

namespace StockIndicators.Application.Account.LinkTelegramChatByDeepLink;

/// <summary>
/// Binds a Telegram chat to the account behind a one-time deep-link token the bot
/// received via <c>/start link_&lt;token&gt;</c>. Only private chats are linked; the bot
/// is not group-capable by design (alerts stay personal).
/// </summary>
public record LinkTelegramChatByDeepLinkCommand(long ChatId, string Token, string? Username) : ICommand<TelegramChatDto>;
