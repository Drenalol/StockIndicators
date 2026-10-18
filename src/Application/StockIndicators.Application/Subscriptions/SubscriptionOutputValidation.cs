using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions;

/// <summary>
/// Shared output-channel rules for subscription create and patch: which channels exist,
/// per-channel uniqueness, the ceiling on how many a subscription may carry, and the
/// normalization of the URL/phone destination. All violations throw ValidationException (400)
/// with a message the client can display.
/// </summary>
public static partial class SubscriptionOutputValidation
{
  /// <summary>Channels that can be attached to a subscription at all.</summary>
  public static readonly NotificationChannel[] SupportedChannels =
  [
    NotificationChannel.Telegram,
    NotificationChannel.WebPush,
    NotificationChannel.Webhook,
    NotificationChannel.Discord,
    NotificationChannel.WhatsApp
  ];

  public static void EnsureSupported(NotificationChannel channel)
  {
    if (!SupportedChannels.Contains(channel))
      throw new ValidationException($"Channel {channel} is not supported", null);
  }

  /// <summary>One output per channel, so the ceiling is the number of supported channels.</summary>
  public static void EnsureWithinChannelLimit(int outputCount)
  {
    const int limit = Limits.MaxOutputChannels;

    if (outputCount > limit)
      throw new ValidationException($"A subscription can have at most {limit} notification outputs", null);
  }

  public static void EnsureAtMostOnePerChannel(IReadOnlyCollection<SubscriptionOutput> outputs)
  {
    var duplicate = outputs.GroupBy(o => o.Channel).FirstOrDefault(g => g.Count() > 1);

    if (duplicate != null)
      throw new ValidationException($"A subscription can have at most one {duplicate.Key} output", null);
  }

  /// <summary>
  /// Normalizes and validates the destination for the channel, returning the value to store:
  /// http(s) URLs for Webhook/Discord (private/loopback hosts need
  /// <paramref name="allowPrivateTargets"/>, the Development escape hatch), E.164 for WhatsApp.
  /// Chat/push channels use ChatId/PushSubscriptionId instead and normalize to null.
  /// </summary>
  public static string? NormalizeTarget(NotificationChannel channel, string? raw, bool allowPrivateTargets)
  {
    switch (channel)
    {
      case NotificationChannel.Webhook:
      case NotificationChannel.Discord:
        return NormalizeUrl(channel, raw, allowPrivateTargets);

      case NotificationChannel.WhatsApp:
        var phone = WhatsAppAlertChannelSender.NormalizePhone(raw);

        if (phone is null)
          throw new ValidationException("WhatsApp output requires a destination phone number in international format (e.g. +15551234567)", null);

        return phone;

      default:
        return null;
    }
  }

  private static string NormalizeUrl(NotificationChannel channel, string? raw, bool allowPrivateTargets)
  {
    var label = channel == NotificationChannel.Discord ? "Discord webhook URL" : "Webhook URL";

    if (string.IsNullOrWhiteSpace(raw))
      throw new ValidationException($"{label} destination is required", null);

    var value = raw.Trim();

    if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
      throw new ValidationException($"{label} must be a valid http(s) URL", null);

    if (uri.Host.Length == 0)
      throw new ValidationException($"{label} must include a host", null);

    if (!allowPrivateTargets && IsPrivateHost(uri.Host))
      throw new ValidationException($"{label} must point to a publicly reachable address", null);

    return value;
  }

  /// <summary>
  /// SSRF guard for server-side POSTs to user-supplied URLs: loopback, private and
  /// link-local literals are rejected (DNS names are not resolved on the request path;
  /// the alert senders are the second line of defense via timeouts).
  /// </summary>
  private static bool IsPrivateHost(string host)
  {
    var hostWithoutBrackets = host.TrimStart('[').TrimEnd(']');

    if (string.Equals(hostWithoutBrackets, "localhost", StringComparison.OrdinalIgnoreCase) || hostWithoutBrackets.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
      return true;

    if (!IPAddress.TryParse(hostWithoutBrackets, out var address))
      return false;

    return IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || IsPrivateV4(address);
  }

  private static bool IsPrivateV4(IPAddress address)
  {
    if (address.AddressFamily != AddressFamily.InterNetwork)
      return false;

    var bytes = address.GetAddressBytes();

    return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168) || bytes[0] == 0 || (bytes[0] == 169 && bytes[1] == 254);
  }
}
