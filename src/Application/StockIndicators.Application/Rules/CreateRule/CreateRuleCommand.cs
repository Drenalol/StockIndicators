using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Rules.CreateRule;

public class CreateRuleCommand : ICommand<Guid>, IHasUserId
{
  public string Name { get; }
  public RuleGroupDto When { get; }
  public RuleActionDto[]? ThenTrue { get; }
  public RuleActionDto[]? ThenFalse { get; }

  /// <summary>
  /// Owner of the rule. Plan limits (conditions per rule, group depth, premium indicators)
  /// are validated against this user's plan.
  /// </summary>
  public Guid UserId { get; }

  public CreateRuleCommand(
    Guid userId,
    string name,
    RuleGroupDto when,
    RuleActionDto[]? thenTrue = null,
    RuleActionDto[]? thenFalse = null
  )
  {
    UserId = userId;
    Name = name;
    When = when;
    ThenTrue = thenTrue;
    ThenFalse = thenFalse;
  }
}
