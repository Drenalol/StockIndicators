using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Evaluates a single rule condition (leaf of a rule group).
/// </summary>
public interface IConditionEvaluator
{
  Task<ConditionOutcome> EvaluateAsync(RuleCondition condition, UserLocale locale, bool ignoreExtendedSessions = true, CancellationToken cancellationToken = default);
}
