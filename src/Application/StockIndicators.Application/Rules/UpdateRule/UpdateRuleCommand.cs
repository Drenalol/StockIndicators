using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Rules.UpdateRule;

public class UpdateRuleCommand : ICommand<Unit>, IHasUserId
{
  public Guid Id { get; }

  /// <summary>
  /// Owner of the rule (plan limits are validated against this user's plan).
  /// </summary>
  public Guid UserId { get; }

  public string Name { get; }
  public RuleGroupDto When { get; }
  public RuleActionDto[]? ThenTrue { get; }
  public RuleActionDto[]? ThenFalse { get; }

  public UpdateRuleCommand(
    Guid userId,
    Guid id,
    string name,
    RuleGroupDto when,
    RuleActionDto[]? thenTrue = null,
    RuleActionDto[]? thenFalse = null
  )
  {
    UserId = userId;
    Id = id;
    Name = name;
    When = when;
    ThenTrue = thenTrue;
    ThenFalse = thenFalse;
  }
}
