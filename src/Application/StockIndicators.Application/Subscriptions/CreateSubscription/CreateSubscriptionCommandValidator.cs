using FluentValidation;
using StockIndicators.Application.Subscriptions.Validators;

namespace StockIndicators.Application.Subscriptions.CreateSubscription;

public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
{
  public CreateSubscriptionCommandValidator()
  {
    RuleFor(command => command.UserId).NotNull();
    RuleFor(command => command.Ticker).Length(1, 10).NotNull();
    RuleFor(command => command.Rule).NotNull().WithMessage("A subscription requires a rule");
    this.CooldownRule(command => command.Cooldown);
    this.MinCooldownRule(command => command.Cooldown);

    // Indicator/template/condition parameters are validated by the nested CreateRuleCommand
    // (RuleValidation), which runs before the subscription row is built
  }
}
