using Microsoft.EntityFrameworkCore;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.NotificationTemplates;

public class NotificationTemplateRepository : INotificationTemplateRepository
{
  private readonly StockIndicatorsContext _stockIndicatorsContext;

  public NotificationTemplateRepository(StockIndicatorsContext stockIndicatorsContext) => _stockIndicatorsContext = stockIndicatorsContext;

  public Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
    _stockIndicatorsContext.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

  // Write-scoped read (AsNoTracking): owned rows plus owner-less legacy rows the caller will
  // claim on this write. Bundled and foreign rows stay invisible so the update path cannot touch them
  public Task<NotificationTemplate?> GetEditableForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
    _stockIndicatorsContext.NotificationTemplates.AsNoTracking()
      .FirstOrDefaultAsync(t => t.Id == id && !t.IsBundled && (t.UserId == null || t.UserId == userId), cancellationToken);

  public async Task<NotificationTemplate> AddAsync(NotificationTemplate template, CancellationToken cancellationToken)
  {
    await _stockIndicatorsContext.NotificationTemplates.AddAsync(template, cancellationToken);
    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
    return template;
  }

  public async Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken)
  {
    // The ownership-scoped read is AsNoTracking: without Update the changes never reach SaveChanges
    _stockIndicatorsContext.NotificationTemplates.Update(template);
    await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
  }

  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
  {
    var template = await _stockIndicatorsContext.NotificationTemplates.FindAsync([id], cancellationToken);

    if (template != null)
    {
      _stockIndicatorsContext.NotificationTemplates.Remove(template);
      await _stockIndicatorsContext.SaveChangesAsync(cancellationToken);
    }
  }
}
