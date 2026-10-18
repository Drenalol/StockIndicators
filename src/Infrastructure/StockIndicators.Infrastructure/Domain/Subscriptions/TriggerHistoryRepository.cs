using Microsoft.EntityFrameworkCore;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

public class TriggerHistoryRepository : ITriggerHistoryRepository
{
  private readonly StockIndicatorsContext _context;

  public TriggerHistoryRepository(StockIndicatorsContext context)
  {
    _context = context;
  }

  public async Task AddAsync(TriggerHistory entry, CancellationToken cancellationToken = default)
  {
    await _context.TriggerHistory.AddAsync(entry, cancellationToken);
  }

  public async Task<TriggerHistory[]> GetLatestBySubscriptionIdAsync(Guid subscriptionId, int limit = 100, CancellationToken cancellationToken = default)
  {
    var entries = await _context.TriggerHistory
      .Where(t => t.SubscriptionId == subscriptionId)
      .OrderByDescending(t => t.TriggeredAt)
      .Take(limit)
      .AsNoTracking()
      .ToListAsync(cancellationToken);

    // Deliveries are loaded separately: a collection Include is not supported on a
    // query that already paginates with Take
    var triggerIds = entries.Select(t => t.Id).ToArray();

    if (triggerIds.Length > 0)
    {
      var deliveries = await _context.TriggerDeliveries
        .Where(d => triggerIds.Contains(d.TriggerHistoryId))
        .AsNoTracking()
        .ToListAsync(cancellationToken);

      foreach (var entry in entries)
        foreach (var delivery in deliveries.Where(d => d.TriggerHistoryId == entry.Id))
          entry.Deliveries.Add(delivery);
    }

    return entries.ToArray();
  }

  public async Task<TriggerHistory?> GetLatestByConditionAsync(Guid subscriptionId, Guid conditionId, CancellationToken cancellationToken = default)
  {
    return await _context.TriggerHistory
      .Where(t => t.SubscriptionId == subscriptionId && t.TriggeredConditionId == conditionId)
      .OrderByDescending(t => t.TriggeredAt)
      .AsNoTracking()
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<TriggerHistory?> GetByIdAsync(Guid id, Guid subscriptionId, CancellationToken cancellationToken = default)
  {
    return await _context.TriggerHistory
      .AsNoTracking()
      .FirstOrDefaultAsync(t => t.Id == id && t.SubscriptionId == subscriptionId, cancellationToken);
  }

  public async Task<TriggerHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
  {
    return await _context.TriggerHistory
      .AsNoTracking()
      .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
  }

  public Task<int> CommitAsync(CancellationToken cancellationToken = default)
  {
    return _context.SaveChangesAsync(cancellationToken);
  }
}
