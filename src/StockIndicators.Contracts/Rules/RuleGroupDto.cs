namespace StockIndicators.Contracts.Rules;

/// <summary>
/// DTO for a rule group: a set of conditions and nested groups combined with a logical operator.
/// </summary>
public record RuleGroupDto(
  Guid? Id,
  LogicalOperatorDto Operator,
  RuleConditionDto[] Conditions,
  RuleGroupDto[] Groups
);
