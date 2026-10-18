using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// A group of alerts resolved for a single delivery target:
/// Telegram — one batch per destination chat (PushSubscriptionId is null),
/// push channels — one batch per push subscription,
/// URL/phone channels — one batch per output with the destination in Target.
/// </summary>
public sealed record AlertBatch(
  NotificationChannel Channel,
  Guid UserId,
  Guid? PushSubscriptionId,
  long? ChatId,
  IReadOnlyList<RenderedAlertItem> Items,
  string Language = UserLocale.DefaultLanguage,
  string? Target = null
);

public sealed record RenderedAlertItem(AlertItem Source, string RenderedText);
