namespace StockIndicators.Domain.Models.NotificationTemplates;

public interface INotificationTemplateRepository
{
  Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

  /// <summary>
  /// Loads a template the caller may write: one they own, or an owner-less legacy row they
  /// will claim on this write. Bundled and other users' templates return null (a foreign row
  /// is indistinguishable from a missing one - no existence leak).
  /// </summary>
  Task<NotificationTemplate?> GetEditableForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);

  Task<NotificationTemplate> AddAsync(NotificationTemplate template, CancellationToken cancellationToken);

  /// <summary>
  /// Marks an ownership-scoped (detached) template as modified; the next <c>SaveChanges</c>
  /// writes its scalar columns (Name/Template/Updated).
  /// </summary>
  Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken);

  Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
