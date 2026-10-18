using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Notifications;

public class NotificationTemplateNotFoundException : NotFoundException
{
  public NotificationTemplateNotFoundException() : base("Notification template not found")
  {
  }
}
