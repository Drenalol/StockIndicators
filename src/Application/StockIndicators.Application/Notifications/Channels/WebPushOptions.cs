namespace StockIndicators.Application.Notifications.Channels;

public class WebPushOptions
{
  public string VapidPublicKey { get; set; } = string.Empty;
  public string VapidPrivateKey { get; set; } = string.Empty;

  /// <summary>VAPID contact (mailto: or https: URL).</summary>
  public string Subject { get; set; } = string.Empty;
}
