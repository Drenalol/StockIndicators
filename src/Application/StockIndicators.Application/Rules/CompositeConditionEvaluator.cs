using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Evaluates rule groups with AND/OR logic, delegating every leaf condition to the
/// simple (single-condition) evaluator. An empty group evaluates to false.
/// </summary>
public class CompositeConditionEvaluator : IRuleEvaluator
{
  private readonly IConditionEvaluator _leafEvaluator;

  public CompositeConditionEvaluator(IConditionEvaluator leafEvaluator) => _leafEvaluator = leafEvaluator;

  public Task<ConditionOutcome> EvaluateAsync(RuleCondition condition, UserLocale locale, bool ignoreExtendedSessions = true, CancellationToken cancellationToken = default) =>
    _leafEvaluator.EvaluateAsync(condition, locale, ignoreExtendedSessions, cancellationToken);

  public async Task<RuleEvaluationResult> EvaluateGroupAsync(RuleGroup group, UserLocale locale, bool ignoreExtendedSessions = true, CancellationToken cancellationToken = default)
  {
    // The group's overall result is AND/OR over its *immediate* children (a sub-group contributes
    // its own aggregated result). Every leaf outcome is collected (flattened) for reporting.
    var childResults = new List<bool>();
    var leafOutcomes = new List<ConditionOutcome>();

    foreach (var condition in group.Conditions)
    {
      var outcome = await _leafEvaluator.EvaluateAsync(condition, locale, ignoreExtendedSessions, cancellationToken);

      leafOutcomes.Add(outcome);
      childResults.Add(outcome.Met);

      if (cancellationToken.IsCancellationRequested)
        break;
    }

    foreach (var subGroup in group.Groups)
    {
      var subResult = await EvaluateGroupAsync(subGroup, locale, ignoreExtendedSessions, cancellationToken);

      childResults.Add(subResult.Met);
      leafOutcomes.AddRange(subResult.Conditions);

      if (cancellationToken.IsCancellationRequested)
        break;
    }

    var met = childResults.Count == 0
      ? false
      : group.LogicalOperator == LogicalOperator.And
        ? childResults.All(result => result)
        : childResults.Any(result => result);

    return new RuleEvaluationResult(met, leafOutcomes);
  }
}
