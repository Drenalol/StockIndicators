using FluentValidation;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Account.UpdateUserPreferences;

public class UpdateUserPreferencesCommandValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
  public UpdateUserPreferencesCommandValidator()
  {
    RuleFor(x => x)
      .Must(x => x.Language is not null || x.Timezone is not null || x.CompleteOnboarding is not null || x.SubscriptionsSort is not null || x.ChartPreferences is not null || x.CompleteSubscriptionTour is not null)
      .WithMessage("At least one of language, timezone, completeOnboarding, subscriptionsSort, chartPreferences or completeSubscriptionTour is required");

    RuleFor(x => x.Language)
      .MaximumLength(10)
      .Must(UserLocale.IsSupportedLanguage)
      .When(x => x.Language is not null)
      .WithMessage($"Unsupported language, supported: {string.Join(", ", UserLocale.SupportedLanguages.Select(descriptor => descriptor.Code))}");

    RuleFor(x => x.Timezone)
      .MaximumLength(64)
      .Must(timezone => TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _))
      .When(x => x.Timezone is not null)
      .WithMessage("Invalid IANA time zone id");

    RuleFor(x => x.SubscriptionsSort)
      .Must(SubscriptionsSort.IsValid)
      .When(x => x.SubscriptionsSort is not null)
      .WithMessage($"Unsupported subscriptions sort, supported: {string.Join(", ", SubscriptionsSort.All)}");
  }
}
