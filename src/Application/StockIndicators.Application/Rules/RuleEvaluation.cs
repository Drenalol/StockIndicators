using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Evaluation outcome of a single leaf condition: whether it was met and a
/// human-readable value (e.g. "MA: 273.2695 (Upward)") for notifications.
/// </summary>
/// <param name="Condition">The leaf condition being evaluated.</param>
/// <param name="Met">Whether the condition fired.</param>
/// <param name="Value">Human-readable value of the outcome (null when unmet).</param>
/// <param name="CandleDate">
/// UTC date of the last candle the condition was evaluated on (null when there were
/// no candles). Anchors the trigger timestamp and its chart to the data that actually
/// fired: the candle cache can lag behind the wall clock, and a wall-clock timestamp
/// would then let later re-renders extend the trigger chart past the signal.
/// </param>
public sealed record ConditionOutcome(RuleCondition Condition, bool Met, string? Value, DateTime? CandleDate = null);

/// <summary>
/// Aggregate evaluation of a rule's WHEN group: the overall result plus every leaf
/// condition's outcome, so consumers can report which conditions fired.
/// </summary>
public sealed class RuleEvaluationResult
{
  public RuleEvaluationResult(bool met, IReadOnlyList<ConditionOutcome> conditions)
  {
    Met = met;
    Conditions = conditions;
  }

  public bool Met { get; }

  /// <summary>All leaf conditions in the tree, in evaluation order.</summary>
  public IReadOnlyList<ConditionOutcome> Conditions { get; }

  /// <summary>The leaf conditions that evaluated true (the ones that contributed to the trigger).</summary>
  public IReadOnlyList<ConditionOutcome> MetConditions => Conditions.Where(condition => condition.Met).ToList();
}
