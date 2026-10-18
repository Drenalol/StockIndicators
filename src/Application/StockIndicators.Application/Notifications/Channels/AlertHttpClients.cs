namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Named HttpClient for user-targeted alert deliveries (generic webhook, Discord, WhatsApp Graph API).
/// </summary>
public static class AlertHttpClients
{
  public const string Delivery = "alert-delivery";
}
