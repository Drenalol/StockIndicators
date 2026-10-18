using System.ComponentModel.DataAnnotations;

namespace StockIndicators.Contracts.Rules;

/// <summary>
/// Full rule update (PATCH /api/rules/{id} replaces the whole rule).
/// </summary>
public class PatchRuleRequest
{
  [Required]
  public string Name { get; set; } = null!;

  [Required]
  public RuleGroupDto When { get; set; } = null!;

  public RuleActionDto[]? ThenTrue { get; set; }

  public RuleActionDto[]? ThenFalse { get; set; }
}
