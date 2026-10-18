namespace StockIndicators.Domain.Models.Subscriptions;

public interface ITriggerSnapshotRepository
{
  Task<TriggerSnapshot[]> GetBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
  Task<TriggerSnapshot?> GetByIdAsync(Guid snapshotId, Guid subscriptionId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Replaces the subscription's stored charts: one row per (ticker, exchange, timeframe)
  /// group. Delete + insert run in a single transaction so a concurrent replace of the same
  /// subscription cannot race into the unique index.
  /// </summary>
  Task ReplaceAsync(Guid subscriptionId, IReadOnlyList<TriggerSnapshot> snapshots, CancellationToken cancellationToken = default);
  Task DeleteBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
  Task DeleteByRuleIdAsync(Guid ruleId, CancellationToken cancellationToken = default);

  Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
