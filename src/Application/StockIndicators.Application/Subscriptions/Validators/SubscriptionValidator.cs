using System.Linq.Expressions;
using FluentValidation;

namespace StockIndicators.Application.Subscriptions.Validators;

/// <summary>
/// Composable validation rules shared by subscription commands. Indicator/instrument/
/// parameter validation is not here: those fields live on the rule and are validated by
/// <c>RuleValidation</c> in the rule commands.
/// </summary>
public static class SubscriptionValidatorRules
{
  public static void CooldownRule<T>(this AbstractValidator<T> validator, Expression<Func<T, TimeSpan>> selector) =>
    validator
      .RuleFor(selector)
      .NotEmpty();

  /// <summary>
  /// Cooldown must leave the check pipeline something to do: below <see cref="Limits.MinCooldown"/>
  /// a subscription would re-alert on every candle of its own timeframe.
  /// </summary>
  public static void MinCooldownRule<T>(this AbstractValidator<T> validator, Expression<Func<T, TimeSpan>> selector) =>
    validator
      .RuleFor(selector)
      .Custom(
        (cooldown, context) =>
        {
          if (cooldown >= Limits.MinCooldown)
            return;

          context.AddFailure("Cooldown", $"Cooldown must be at least {(int)Limits.MinCooldown.TotalSeconds} seconds");
        }
      );
}
