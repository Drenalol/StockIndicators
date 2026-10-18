using Microsoft.Extensions.Options;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Account.CreateTelegramLinkToken;

public class CreateTelegramLinkTokenCommandHandler : ICommandHandler<CreateTelegramLinkTokenCommand, TelegramLinkDto>
{
  private readonly IUserRepository _userRepository;
  private readonly ITelegramLinkTokenStore _linkTokenStore;
  private readonly TelegramBotOptions _botOptions;

  public CreateTelegramLinkTokenCommandHandler(
    IUserRepository userRepository,
    ITelegramLinkTokenStore linkTokenStore,
    IOptions<TelegramBotOptions> botOptions
  )
  {
    _userRepository = userRepository;
    _linkTokenStore = linkTokenStore;
    _botOptions = botOptions.Value;
  }

  public async Task<TelegramLinkDto> Handle(CreateTelegramLinkTokenCommand request, CancellationToken cancellationToken)
  {
    if (await _userRepository.GetByIdAsync(request.UserId, cancellationToken) is null)
      throw new UserNotFoundException();

    var issued = await _linkTokenStore.IssueAsync(request.UserId, cancellationToken);

    var parameter = TelegramLinkTokenOptions.StartParameter(issued.Token);

    return new TelegramLinkDto(
      _botOptions.DeepLink(parameter),
      issued.ExpiresAt);
  }
}
