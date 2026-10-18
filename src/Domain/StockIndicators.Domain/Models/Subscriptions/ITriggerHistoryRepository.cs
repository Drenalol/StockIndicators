namespace StockIndicators.Domain.Models.Subscriptions;

public interface ITriggerHistoryRepository
{
  Task AddAsync(TriggerHistory entry, CancellationToken cancellationToken = default);
  Task<TriggerHistory[]> GetLatestBySubscriptionIdAsync(Guid subscriptionId, int limit = 100, CancellationToken cancellationToken = default);
  Task<TriggerHistory?> GetLatestByConditionAsync(Guid subscriptionId, Guid conditionId, CancellationToken cancellationToken = default);
  Task<TriggerHistory?> GetByIdAsync(Guid id, Guid subscriptionId, CancellationToken cancellationToken = default);
  Task<TriggerHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
  Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
