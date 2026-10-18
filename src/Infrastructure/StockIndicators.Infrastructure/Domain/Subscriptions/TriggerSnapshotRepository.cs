using Microsoft.EntityFrameworkCore;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

public class TriggerSnapshotRepository : ITriggerSnapshotRepository
{
  private readonly StockIndicatorsContext _context;

  public TriggerSnapshotRepository(StockIndicatorsContext context)
  {
    _context = context;
  }

  public async Task<TriggerSnapshot[]> GetBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
    await _context.TriggerSnapshots
      .AsNoTracking()
      .Where(snapshot => snapshot.SubscriptionId == subscriptionId)
      .OrderBy(snapshot => snapshot.Ticker)
      .ThenBy(snapshot => snapshot.ExchangeName)
      .ThenBy(snapshot => snapshot.TimeFrame)
      .ToArrayAsync(cancellationToken);

  public async Task<TriggerSnapshot?> GetByIdAsync(Guid snapshotId, Guid subscriptionId, CancellationToken cancellationToken = default) =>
    await _context.TriggerSnapshots
      .AsNoTracking()
      .FirstOrDefaultAsync(snapshot => snapshot.Id == snapshotId && snapshot.SubscriptionId == subscriptionId, cancellationToken);

  public async Task ReplaceAsync(Guid subscriptionId, IReadOnlyList<TriggerSnapshot> snapshots, CancellationToken cancellationToken = default)
  {
    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

    // Serialize concurrent replaces of the same subscription: a double page-load on a cache
    // miss would otherwise race the delete + insert into a unique violation
    await _context.Database.ExecuteSqlInterpolatedAsync(
      $"""SELECT pg_advisory_xact_lock(Hashtext({subscriptionId.ToString()}))""",
      cancellationToken);

    await _context.TriggerSnapshots
      .Where(snapshot => snapshot.SubscriptionId == subscriptionId)
      .ExecuteDeleteAsync(cancellationToken);

    if (snapshots.Count > 0)
    {
      _context.TriggerSnapshots.AddRange(snapshots);
      await _context.SaveChangesAsync(cancellationToken);
    }

    await transaction.CommitAsync(cancellationToken);
  }

  public Task DeleteBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
    _context.TriggerSnapshots
      .Where(snapshot => snapshot.SubscriptionId == subscriptionId)
      .ExecuteDeleteAsync(cancellationToken);

  public Task DeleteByRuleIdAsync(Guid ruleId, CancellationToken cancellationToken = default) =>
    _context.TriggerSnapshots
      .Where(snapshot => _context.Subscriptions
        .Where(subscription => subscription.RuleGroupId == ruleId)
        .Select(subscription => subscription.Id)
        .Contains(snapshot.SubscriptionId))
      .ExecuteDeleteAsync(cancellationToken);

  public Task<int> CommitAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
