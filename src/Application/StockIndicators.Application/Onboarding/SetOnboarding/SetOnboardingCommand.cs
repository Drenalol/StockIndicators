using System.Text.Json;
using MediatR;

namespace StockIndicators.Application.Onboarding.SetOnboarding;

/// <summary>Stores the onboarding dismissal flag; hiding and un-hiding the guide are the same write.</summary>
public record SetOnboardingCommand(bool Hidden) : IRequest;

public class SetOnboardingCommandHandler : IRequestHandler<SetOnboardingCommand>
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private readonly IAppSettingsStore _store;

  public SetOnboardingCommandHandler(IAppSettingsStore store) => _store = store;

  public Task Handle(SetOnboardingCommand request, CancellationToken cancellationToken) =>
    _store.SetAsync(
      GetOnboarding.GetOnboardingQueryHandler.StateKey,
      JsonSerializer.Serialize(new { request.Hidden }, JsonOptions),
      cancellationToken);
}
