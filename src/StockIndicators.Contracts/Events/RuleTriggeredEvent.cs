namespace StockIndicators.Contracts.Events;

/// <summary>
/// Published when a rule is evaluated and its THEN-actions ran. The subscription itself
/// carries no instrument (it is a container of the rule), so consumers read the watched
/// instruments from the rule's conditions.
/// </summary>
public record RuleTriggeredEvent(
  Guid RuleId,
  string RuleName,
  Guid SubscriptionId,
  Guid UserId,
  bool ConditionMet,
  string Actions,
  DateTime OccurredAt
);
