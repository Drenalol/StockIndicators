using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Indicators;

/// <summary>
/// Postgres store for trailing-stop anchors. Every operation opens its own DI scope: the
/// check pipeline reuses one scoped DbContext across a batch (trigger history commits at
/// the end of it), and an anchor write-through must never flush those unrelated tracked
/// changes early - its SaveChanges only ever touches its own context instance.
/// </summary>
public class TrailingStopAnchorStore : ITrailingStopAnchorStore
{
  private readonly IServiceScopeFactory _scopeFactory;

  public TrailingStopAnchorStore(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

  public async Task<double?> GetAnchorAsync(Guid conditionId, CancellationToken cancellationToken = default)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<StockIndicatorsContext>();

    return await context.TrailingStopAnchors
      .Where(anchor => anchor.ConditionId == conditionId)
      .Select(anchor => (double?)anchor.Anchor)
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task SetAnchorAsync(Guid conditionId, double anchor, CancellationToken cancellationToken = default)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<StockIndicatorsContext>();

    var row = await context.TrailingStopAnchors.FindAsync([conditionId], cancellationToken);

    if (row is null)
      await context.TrailingStopAnchors.AddAsync(
        new TrailingStopAnchor { ConditionId = conditionId, Anchor = anchor, Updated = DateTime.UtcNow }, cancellationToken);
    else
    {
      row.Anchor = anchor;
      row.Updated = DateTime.UtcNow;
    }

    await context.SaveChangesAsync(cancellationToken);
  }

  public async Task RemoveAnchorsAsync(IReadOnlyCollection<Guid> conditionIds, CancellationToken cancellationToken = default)
  {
    if (conditionIds.Count == 0)
      return;

    await using var scope = _scopeFactory.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<StockIndicatorsContext>();

    await context.TrailingStopAnchors
      .Where(anchor => conditionIds.Contains(anchor.ConditionId))
      .ExecuteDeleteAsync(cancellationToken);
  }
}
