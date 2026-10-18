using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;

namespace StockIndicators.Application.Notifications.Rendering;

/// <summary>
/// Renders a single alert line. Without a template, falls back to the channel default:
/// Telegram — HTML bullet line (legacy format), push channels — plain alert text.
/// Template tokens: {{ticker}}, {{exchange}}, {{indicator}}, {{alertText}}, {{time}}, {{subscriptionId}}.
/// {{time}} is formatted in the user's timezone.
/// </summary>
public sealed class AlertTemplateRenderer : IAlertTemplateRenderer
{
  public string RenderLine(NotificationChannel channel, NotificationTemplate? template, AlertItem item, UserLocale locale)
  {
    if (template == null)
      return channel == NotificationChannel.Telegram ? DefaultTelegramLine(item) : item.AlertText;

    return template.Template
      .Replace("{{ticker}}", item.InstrumentTicker)
      .Replace("{{exchange}}", item.ExchangeName ?? string.Empty)
      .Replace("{{indicator}}", item.IndicatorName)
      .Replace("{{alertText}}", item.AlertText)
      .Replace("{{time}}", AlertTexts.FormatDate(item.TriggeredAt, locale.Timezone, "yyyy-MM-dd HH:mm"))
      .Replace("{{subscriptionId}}", item.SubscriptionId.ToString());
  }

  private static string DefaultTelegramLine(AlertItem item) => $"• <b>{item.InstrumentTicker}</b> ({item.IndicatorName}): {item.AlertText}";
}
