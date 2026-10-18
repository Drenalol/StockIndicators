using StockIndicators.Application.Charts;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Single source of truth for the alert deep-link path used by every channel:
/// the subscription page and its trigger history (<c>?view=history</c> scroll flag).
/// The ticker is escaped because crypto pairs carry a slash ("SOL/USDT").
/// </summary>
public static class AlertDeepLinks
{
  /// <summary>Dashboard-namespaced path without the locale prefix (channels prepend their own base).</summary>
  public static string SubscriptionPath(string ticker, Guid subscriptionId, bool history = false) =>
    $"dashboard/subscriptions/{Uri.EscapeDataString(ticker)}/{subscriptionId}{(history ? "?view=history" : "")}";

  /// <summary>
  /// HMAC-signed public trigger chart URL, or null when the alert image options
  /// (PublicBaseUrl/ImageSigningKey) are not configured.
  /// </summary>
  public static string? BuildChartImageUrl(AlertOptions options, Guid triggerId)
  {
    if (string.IsNullOrEmpty(options.PublicBaseUrl) || string.IsNullOrEmpty(options.ImageSigningKey))
      return null;

    return TriggerChartImages.BuildSignedUrl(options.PublicBaseUrl, options.ImageSigningKey, triggerId);
  }
}
