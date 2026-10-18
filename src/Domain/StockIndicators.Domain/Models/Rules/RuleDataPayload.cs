namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Serializable shape of the rule tree, stored in <see cref="Rule.RuleData"/>.
/// </summary>
public class RuleDataPayload
{
  public RuleGroup When { get; set; } = new();
  public List<RuleAction> ThenTrue { get; set; } = [];
  public List<RuleAction> ThenFalse { get; set; } = [];
}
