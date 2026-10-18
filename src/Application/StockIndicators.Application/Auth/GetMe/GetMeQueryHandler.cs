using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Telegram;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Auth.GetMe;

public class GetMeQueryHandler : IQueryHandler<GetMeQuery, AuthProfileDto>
{
  private readonly IUserRepository _userRepository;
  private readonly ITelegramChatRepository _telegramChatRepository;

  public GetMeQueryHandler(
    IUserRepository userRepository,
    ITelegramChatRepository telegramChatRepository
  )
  {
    _userRepository = userRepository;
    _telegramChatRepository = telegramChatRepository;
  }

  public async Task<AuthProfileDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
      ?? throw new UserNotFoundException();

    var chats = await _telegramChatRepository.GetByUserIdAsync(request.UserId, cancellationToken);

    return new AuthProfileDto(
      user.Id.ToString(),
      user.DisplayName,
      user.Language,
      user.Timezone,
      user.InitOnboardingCompletedAt is not null,
      [.. chats.Select(chat => new TelegramChatDto(
        chat.Id.ToString(),
        chat.ChatId,
        chat.Username,
        chat.Created))],
      SubscriptionsSort: user.SubscriptionsSort,
      ChartPreferences: ChartPreferencesDto.Parse(user.ChartPreferences),
      SubscriptionTourCompleted: user.SubscriptionOnboardingCompletedAt is not null);
  }
}
