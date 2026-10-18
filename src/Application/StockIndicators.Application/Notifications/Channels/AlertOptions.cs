namespace StockIndicators.Application.Notifications.Channels;

public class AlertOptions
{
  /// <summary>
  /// Externally reachable base URL of the API (no trailing slash). When empty,
  /// web push alerts are sent as text only (no image URL can be delivered).
  /// </summary>
  public string PublicBaseUrl { get; set; } = string.Empty;

  /// <summary>
  /// Shared secret for HMAC-signing public trigger image URLs. When empty,
  /// web push alerts are sent as text only.
  /// </summary>
  public string ImageSigningKey { get; set; } = string.Empty;

  /// <summary>
  /// Allows Webhook/Discord destinations to point at loopback/private addresses.
  /// A Development escape hatch for local webhook testing; must stay off in production.
  /// </summary>
  public bool AllowPrivateWebhookTargets { get; set; }
}
