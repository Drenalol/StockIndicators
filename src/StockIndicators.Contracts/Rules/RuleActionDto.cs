using Newtonsoft.Json.Linq;

namespace StockIndicators.Contracts.Rules;

/// <summary>
/// DTO for a THEN-action of a rule.
/// </summary>
public record RuleActionDto(
  Guid? Id,
  ActionTypeDto Type,
  Guid? NotificationTemplateId,
  JToken? Parameters
);
