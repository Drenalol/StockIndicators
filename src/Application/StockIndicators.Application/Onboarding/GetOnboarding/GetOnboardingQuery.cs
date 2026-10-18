using System.Text.Json;
using MediatR;
using StockIndicators.Contracts.Onboarding;

namespace StockIndicators.Application.Onboarding.GetOnboarding;

public record GetOnboardingQuery : IRequest<OnboardingDto>;

/// <summary>
/// Reads the onboarding dismissal flag. Unparseable stored JSON reads as "not dismissed":
/// a broken value costs the operator one guide, not the guide forever.
/// </summary>
public class GetOnboardingQueryHandler : IRequestHandler<GetOnboardingQuery, OnboardingDto>
{
  public const string StateKey = "onboarding";

  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private readonly IAppSettingsStore _store;

  public GetOnboardingQueryHandler(IAppSettingsStore store) => _store = store;

  public async Task<OnboardingDto> Handle(GetOnboardingQuery request, CancellationToken cancellationToken)
  {
    var json = await _store.GetAsync(StateKey, cancellationToken);

    if (json is null)
      return new OnboardingDto(false);

    try
    {
      return JsonSerializer.Deserialize<OnboardingDto>(json, JsonOptions) ?? new OnboardingDto(false);
    }
    catch (JsonException)
    {
      return new OnboardingDto(false);
    }
  }
}
