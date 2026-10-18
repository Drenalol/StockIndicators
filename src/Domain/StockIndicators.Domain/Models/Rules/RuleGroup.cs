namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Logical group of conditions with AND/OR grouping. The root group Id equals the owning Rule.Id.
/// </summary>
public class RuleGroup
{
  public Guid Id { get; set; }

  /// <summary>Logical operator combining child conditions/groups</summary>
  public LogicalOperator LogicalOperator { get; set; }

  /// <summary>Direct conditions in this group</summary>
  public IList<RuleCondition> Conditions { get; set; } = [];

  /// <summary>Nested groups (recursive for multi-level AND/OR)</summary>
  public IList<RuleGroup> Groups { get; set; } = [];

  /// <summary>
  /// Distinct instruments (ticker + exchange) watched by all leaf conditions of this tree,
  /// keyed under the root rule id. Feeds the <see cref="RuleInstrument"/> projection.
  /// </summary>
  public IReadOnlyCollection<RuleInstrument> CollectInstruments(Guid ruleId)
  {
    var instruments = new HashSet<(string Ticker, string ExchangeName)>();
    Collect(this);

    return instruments.Select(i => new RuleInstrument { RuleId = ruleId, Ticker = i.Ticker, ExchangeName = i.ExchangeName })
      .ToArray();

    void Collect(RuleGroup group)
    {
      foreach (var condition in group.Conditions)
        instruments.Add((condition.Ticker, condition.ExchangeName ?? string.Empty));

      foreach (var subGroup in group.Groups)
        Collect(subGroup);
    }
  }

  /// <summary>
  /// Distinct (ticker, exchange, indicator) triples of all leaf conditions of this tree,
  /// keyed under the root rule id. Feeds the <see cref="RuleIndicator"/> projection.
  /// </summary>
  public IReadOnlyCollection<RuleIndicator> CollectIndicators(Guid ruleId)
  {
    var indicators = new HashSet<(string Ticker, string ExchangeName, Models.Indicators.Indicator Indicator)>();
    Collect(this);

    return indicators
      .Select(i => new RuleIndicator { RuleId = ruleId, Ticker = i.Ticker, ExchangeName = i.ExchangeName, Indicator = i.Indicator })
      .ToArray();

    void Collect(RuleGroup group)
    {
      foreach (var condition in group.Conditions)
        indicators.Add((condition.Ticker, condition.ExchangeName ?? string.Empty, condition.Indicator));

      foreach (var subGroup in group.Groups)
        Collect(subGroup);
    }
  }

  /// <summary>Total leaf-condition count of this group tree (flat rule = 1).</summary>
  public int CountConditions() => Conditions.Count + Groups.Sum(g => g.CountConditions());

  /// <summary>
  /// True when the tree watches more than one distinct instrument (ticker + exchange).
  /// </summary>
  public bool WatchesMultipleInstruments() =>
    CollectInstruments(Guid.Empty)
      .Select(instrument => (instrument.Ticker.ToLowerInvariant(), instrument.ExchangeName.ToLowerInvariant()))
      .Distinct()
      .Count() > 1;

  /// <summary>Finds a leaf condition by id anywhere in the group tree.</summary>
  public RuleCondition? FindCondition(Guid id)
  {
    foreach (var condition in Conditions)
    {
      if (condition.Id == id)
        return condition;
    }

    foreach (var subGroup in Groups)
    {
      var found = subGroup.FindCondition(id);

      if (found is not null)
        return found;
    }

    return null;
  }
}

/// <summary>
/// Logical operators for combining conditions.
/// </summary>
public enum LogicalOperator
{
  And,
  Or
}
