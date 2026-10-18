using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Auth;

namespace StockIndicators.Application.Account.GetTelegramChats;

public record GetTelegramChatsQuery(Guid UserId) : IQuery<IReadOnlyList<TelegramChatDto>>;
