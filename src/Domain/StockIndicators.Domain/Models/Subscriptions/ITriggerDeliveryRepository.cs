namespace StockIndicators.Domain.Models.Subscriptions;

public interface ITriggerDeliveryRepository
{
  Task AddAsync(TriggerDelivery delivery, CancellationToken cancellationToken = default);
  Task AddRangeAsync(IReadOnlyCollection<TriggerDelivery> deliveries, CancellationToken cancellationToken = default);
}
