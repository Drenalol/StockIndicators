using System.ComponentModel.DataAnnotations;

namespace StockIndicators.Contracts.Rules;

public class CreateRuleRequest
{
  [Required]
  public string Name { get; set; } = null!;

  [Required]
  public RuleGroupDto When { get; set; } = null!;

  public RuleActionDto[]? ThenTrue { get; set; }

  public RuleActionDto[]? ThenFalse { get; set; }
}
