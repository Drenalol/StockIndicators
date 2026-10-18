using System.Text.Json;
using MediatR;
using StockIndicators.Application.Auth.GetMe;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Account.UpdateUserPreferences;

public class UpdateUserPreferencesCommandHandler : ICommandHandler<UpdateUserPreferencesCommand, AuthProfileDto>
{
  private readonly IUserRepository _userRepository;
  private readonly IMediator _mediator;

  public UpdateUserPreferencesCommandHandler(IUserRepository userRepository, IMediator mediator)
  {
    _userRepository = userRepository;
    _mediator = mediator;
  }

  public async Task<AuthProfileDto> Handle(UpdateUserPreferencesCommand request, CancellationToken cancellationToken)
  {
    var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
      ?? throw new UserNotFoundException();

    if (request.Language is { } language)
      user.Language = language;

    if (request.Timezone is { } timezone)
      user.Timezone = timezone;

    if (request.CompleteOnboarding == true)
      user.InitOnboardingCompletedAt ??= DateTime.UtcNow;

    if (request.CompleteSubscriptionTour == true)
      user.SubscriptionOnboardingCompletedAt ??= DateTime.UtcNow;

    if (request.SubscriptionsSort is { } subscriptionsSort)
      user.SubscriptionsSort = subscriptionsSort;

    if (request.ChartPreferences is { } chartPreferences)
    {
      // Partial merge into the stored jsonb document; an all-null result stores null ("never set")
      var merged = chartPreferences.Merge(ChartPreferencesDto.Parse(user.ChartPreferences));
      user.ChartPreferences = merged is null ? null : JsonSerializer.Serialize(merged);
    }

    await _userRepository.CommitAsync(cancellationToken);

    return await _mediator.Send(new GetMeQuery(request.UserId), cancellationToken);
  }
}
