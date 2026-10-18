namespace StockIndicators.Application.Notifications.GetNotificationTemplates;

public class NotificationTemplateDto
{
  public Guid Id { get; set; }
  public string Name { get; set; } = null!;
  public bool IsBundled { get; set; }
  public string Template { get; set; } = null!;
  /// <summary>
  /// True when the caller may edit/delete the template: their own, or an owner-less
  /// legacy row (first write claims it for them). Bundled templates are never editable.
  /// </summary>
  public bool CanEdit { get; set; }
}
