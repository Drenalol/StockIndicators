namespace StockIndicators.Domain.Models.Notifications;

/// <summary>
/// Alert delivery channels. Email/SignalR and MobilePush are reserved for future senders.
/// Channels are persisted as strings, so new members must be appended without renumbering.
/// </summary>
public enum NotificationChannel
{
  Telegram = 0,
  Email = 1,
  Webhook = 2,
  SignalR = 3,
  WebPush = 4,
  MobilePush = 5,
  WhatsApp = 6,
  Discord = 7
}
