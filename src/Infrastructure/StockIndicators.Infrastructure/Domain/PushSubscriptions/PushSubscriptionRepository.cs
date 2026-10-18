using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.PushSubscriptions;

public class PushSubscriptionRepository : IPushSubscriptionRepository
{
  private readonly StockIndicatorsContext _stockIndicatorsContext;

  public PushSubscriptionRepository(StockIndicatorsContext stockIndicatorsContext) => _stockIndicatorsContext = stockIndicatorsContext;

  public async Task<PushSubscription?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
    await _stockIndicatorsContext.PushSubscriptions.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);

  public async Task<List<PushSubscription>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
    await _stockIndicatorsContext.PushSubscriptions.Where(p => p.UserId == userId).ToListAsync(cancellationToken);

  public async Task<PushSubscription> UpsertAsync(PushSubscription pushSubscription, CancellationToken cancellationToken)
  {
    try
    {
      await _stockIndicatorsContext.PushSubscriptions.AddAsync(pushSubscription, cancellationToken);
      await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
      return pushSubscription;
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
      // Lost the insert race: another request committed the same (UserId, Endpoint) first
      return await RefreshExistingAsync(pushSubscription, cancellationToken);
    }
  }

  private async Task<PushSubscription> RefreshExistingAsync(PushSubscription incoming, CancellationToken cancellationToken)
  {
    // Detach the failed insert so SaveChanges does not retry it
    _stockIndicatorsContext.Entry(incoming).State = EntityState.Detached;

    var existing = await _stockIndicatorsContext.PushSubscriptions
      .FirstOrDefaultAsync(p => p.UserId == incoming.UserId && p.Endpoint == incoming.Endpoint, cancellationToken);

    if (existing == null)
      throw new InvalidOperationException($"PushSubscription ({incoming.UserId}, {incoming.Endpoint}) vanished after unique violation");

    existing.PublicKey = incoming.PublicKey;
    existing.AuthKey = incoming.AuthKey;

    if (incoming.DeviceLabel != null)
      existing.DeviceLabel = incoming.DeviceLabel;

    existing.Updated = DateTime.UtcNow;
    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);

    return existing;
  }

  public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
  {
    var pushSubscription = await GetByIdAsync(id, userId, cancellationToken);

    if (pushSubscription is null)
      return;

    _stockIndicatorsContext.PushSubscriptions.Remove(pushSubscription);

    // SubscriptionOutputs reference the push subscription as their WebPush target with
    // ON DELETE SET NULL: without this they stay enabled with a null target forever and the
    // alert dispatcher keeps skipping their alerts with a warning. Disable them instead;
    // re-enabling goes through PATCH, which demands a fresh live push subscription.
    var outputs = await _stockIndicatorsContext.SubscriptionOutputs
      .Where(o => o.PushSubscriptionId == id)
      .ToListAsync(cancellationToken);

    foreach (var output in outputs)
      output.IsEnabled = false;
  }

  public Task<int> CommitAsync(CancellationToken cancellationToken) => _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
}
