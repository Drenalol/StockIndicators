using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Rules;

public interface IRuleActionExecutor
{
  /// <summary>
  /// Executes the rule's THEN-actions for the given evaluation result.
  /// Returns true when actions ran (the caller applies the subscription cooldown). A notification
  /// suppressed as a same-candle duplicate returns false — the executor has already armed the
  /// cooldown itself, without touching the unread stamp.
  /// </summary>
  Task<bool> ExecuteAsync(Rule rule, RuleEvaluationResult evaluation, Subscription subscription, UserLocale locale, CancellationToken cancellationToken = default);
}
