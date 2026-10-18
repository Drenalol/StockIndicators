using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Repositories;

namespace StockIndicators.Domain.Models.Rules;

public interface IRuleRepository : IRepositoryBase<Rule, Guid>
{
  Task<Rule[]> GetAllAsync(CancellationToken cancellationToken);

  /// <summary>
  /// The distinct instruments (ticker + exchange) currently persisted for the rule
  /// (<see cref="RuleInstrument"/> projection). Read it before an update whose previous
  /// instrument set must be reconciled with provider tracking afterwards.
  /// </summary>
  Task<InstrumentKey[]> GetInstrumentsAsync(Guid ruleId, CancellationToken cancellationToken);

  /// <summary>
  /// Loads a rule only when it belongs to <paramref name="userId"/>; otherwise null
  /// (a foreign rule is indistinguishable from a missing one — no existence leak).
  /// </summary>
  Task<Rule?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);

  /// <summary>
  /// Marks an ownership-scoped (detached) rule as modified so the next <c>CommitAsync</c>
  /// writes its scalar columns (Name/RuleData/Updated); the tree itself lives in RuleData.
  /// </summary>
  Task UpdateAsync(Rule rule, CancellationToken cancellationToken);

  /// <summary>
  /// True when any subscription (inactive ones included — the FK is Restrict) references the rule.
  /// </summary>
  Task<bool> IsReferencedBySubscriptionAsync(Guid ruleId, CancellationToken cancellationToken);
}
