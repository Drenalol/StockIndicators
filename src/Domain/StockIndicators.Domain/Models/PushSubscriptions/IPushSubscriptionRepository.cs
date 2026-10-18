namespace StockIndicators.Domain.Models.PushSubscriptions;

public interface IPushSubscriptionRepository
{
  Task<PushSubscription?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);
  Task<List<PushSubscription>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
  /// <summary>
  /// Idempotent upsert by (UserId, Endpoint): inserts a new row or refreshes keys/device label
  /// on the existing one. Safe under concurrency (relies on the unique index).
  /// </summary>
  Task<PushSubscription> UpsertAsync(PushSubscription pushSubscription, CancellationToken cancellationToken);
  Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
  Task<int> CommitAsync(CancellationToken cancellationToken);
}
