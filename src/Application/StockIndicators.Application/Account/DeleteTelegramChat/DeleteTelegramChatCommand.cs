using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Account.DeleteTelegramChat;

public record DeleteTelegramChatCommand(Guid UserId, Guid ChatId) : ICommand<Unit>;
