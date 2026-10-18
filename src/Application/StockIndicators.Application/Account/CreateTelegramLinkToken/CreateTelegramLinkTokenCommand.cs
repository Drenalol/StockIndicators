using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Contracts.Auth;

namespace StockIndicators.Application.Account.CreateTelegramLinkToken;

public record CreateTelegramLinkTokenCommand(Guid UserId) : ICommand<TelegramLinkDto>;
