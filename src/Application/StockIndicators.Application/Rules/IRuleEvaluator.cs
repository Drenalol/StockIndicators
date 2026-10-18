using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Evaluates rule conditions against market data.
/// </summary>
public interface IRuleEvaluator
{
  /// <summary>
  /// Evaluates a single condition against the latest candles.
  /// <paramref name="ignoreExtendedSessions"/> comes from the subscription the rule is
  /// evaluated for: when true, a condition whose newest candle belongs to an extended
  /// session is silently unmet (the subscription opted out of extended-hours alerts).
  /// </summary>
  Task<ConditionOutcome> EvaluateAsync(RuleCondition condition, UserLocale locale, bool ignoreExtendedSessions = true, CancellationToken cancellationToken = default);

  /// <summary>
  /// Evaluates a group of conditions with AND/OR logic, returning the overall result and every
  /// leaf condition's outcome (so consumers can report which conditions fired).
  /// </summary>
  Task<RuleEvaluationResult> EvaluateGroupAsync(RuleGroup group, UserLocale locale, bool ignoreExtendedSessions = true, CancellationToken cancellationToken = default);
}
