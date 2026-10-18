namespace StockIndicators.Contracts.Rules;

/// <summary>
/// Full rule: WHEN group + THEN-true / THEN-false actions.
/// </summary>
public record RuleDto(
  Guid Id,
  string Name,
  RuleGroupDto When,
  RuleActionDto[] ThenTrue,
  RuleActionDto[] ThenFalse
);
