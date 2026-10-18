using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Contracts.Auth;

namespace StockIndicators.Application.Account.UpdateUserPreferences;

public record UpdateUserPreferencesCommand(
  Guid UserId,
  string? Language,
  string? Timezone,
  bool? CompleteOnboarding = null,
  string? SubscriptionsSort = null,
  ChartPreferencesDto? ChartPreferences = null,
  bool? CompleteSubscriptionTour = null
) : ICommand<AuthProfileDto>;
