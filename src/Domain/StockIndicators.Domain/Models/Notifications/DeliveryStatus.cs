namespace StockIndicators.Domain.Models.Notifications;

/// <summary>
/// Result of a single alert delivery attempt to one channel. Persisted as a string.
/// </summary>
public enum DeliveryStatus
{
  /// <summary>The channel accepted the alert (HTTP 2xx / Telegram accepted the message).</summary>
  Sent = 0,

  /// <summary>The delivery was attempted and failed; the error text explains why.</summary>
  Failed = 1,

  /// <summary>The alert was not sent on purpose (channel unavailable, alert superseded).</summary>
  Skipped = 2
}
