using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;

namespace StockIndicators.Application.Notifications.Rendering;

public interface IAlertTemplateRenderer
{
  string RenderLine(NotificationChannel channel, NotificationTemplate? template, AlertItem item, UserLocale locale);
}
