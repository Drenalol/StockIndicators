namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// A WHEN-THEN rule. <see cref="When"/> is the root rule group (AND/OR tree of conditions),
/// <see cref="ThenTrue"/>/<see cref="ThenFalse"/> are the actions to run depending on the result.
/// </summary>
public class Rule : UpdatableEntity<Guid>
{
  /// <summary>
  /// Owner of the rule. Every user-facing access is scoped by this id.
  /// </summary>
  public Guid UserId { get; set; }

  public string Name { get; set; } = null!;

  /// <summary>
  /// Serialized rule tree (WHEN group + THEN actions). Stored as jsonb.
  /// </summary>
  public string RuleData { get; set; } = null!;

  /// <summary>
  /// Leaf-condition count of the WHEN tree, maintained by the repository next to the
  /// RuleInstruments/RuleIndicators projections: lets the subscription list tell a flat
  /// rule (1) from a multi-condition one (&gt;1) without parsing the jsonb.
  /// </summary>
  public int ConditionCount { get; set; }

  // --- The properties below are deserialized by the infrastructure layer; EF ignores them ---

  /// <summary>
  /// Root rule group (WHEN). Its Id equals <see cref="Entity{TId}.Id"/> of this rule.
  /// </summary>
  public RuleGroup When { get; set; } = new();

  /// <summary>
  /// Actions executed when the rule group evaluates to true.
  /// </summary>
  public IList<RuleAction> ThenTrue { get; set; } = [];

  /// <summary>
  /// Actions executed when the rule group evaluates to false.
  /// </summary>
  public IList<RuleAction> ThenFalse { get; set; } = [];
}
