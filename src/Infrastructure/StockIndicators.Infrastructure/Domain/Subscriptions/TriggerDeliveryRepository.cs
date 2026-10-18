using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

/// <summary>
/// Only queues deliveries: they are committed together with the trigger history row
/// of the same scheduler run (ITriggerHistoryRepository.CommitAsync).
/// </summary>
public class TriggerDeliveryRepository : ITriggerDeliveryRepository
{
  private readonly StockIndicatorsContext _context;

  public TriggerDeliveryRepository(StockIndicatorsContext context) => _context = context;

  public Task AddAsync(TriggerDelivery delivery, CancellationToken cancellationToken = default) =>
    _context.TriggerDeliveries.AddAsync(delivery, cancellationToken).AsTask();

  public Task AddRangeAsync(IReadOnlyCollection<TriggerDelivery> deliveries, CancellationToken cancellationToken = default) =>
    _context.TriggerDeliveries.AddRangeAsync(deliveries, cancellationToken);
}
