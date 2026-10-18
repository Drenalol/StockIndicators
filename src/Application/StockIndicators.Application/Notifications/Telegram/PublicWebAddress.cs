using System.Net;
using System.Net.Sockets;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Whether an address is one that can be opened from outside the operator's own network.
/// Telegram checks every link button on its own servers and refuses loopback and
/// single-label names ("Wrong HTTP URL"), and a LAN address is dead on any device that is
/// off that network, so a link built from one never reaches a working page.
/// </summary>
public static class PublicWebAddress
{
  /// <summary>Names that resolve only inside a private network (mDNS, LLMNR, home routers).</summary>
  private static readonly string[] LocalSuffixes = [".local", ".localdomain", ".internal", ".lan", ".home.arpa"];

  public static bool IsReachable(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
      return false;

    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
      return false;

    // an IPv6 literal keeps its brackets in Host
    var host = uri.Host.TrimStart('[').TrimEnd(']').ToLowerInvariant();

    if (host.Length == 0)
      return false;

    if (IPAddress.TryParse(host, out var address))
      return IsPublicIp(address);

    // "nas" or "router" without a suffix resolves on the LAN only
    if (!host.Contains('.'))
      return false;

    if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal))
      return false;

    return LocalSuffixes.All(suffix => !host.EndsWith(suffix, StringComparison.Ordinal));
  }

  private static bool IsPublicIp(IPAddress address)
  {
    if (IPAddress.IsLoopback(address))
      return false;

    if (address.AddressFamily == AddressFamily.InterNetwork)
      return IsPublicIPv4(address.GetAddressBytes());

    var bytes = address.GetAddressBytes();

    // the unspecified :: never resolves anywhere
    if (bytes.All(byteValue => byteValue == 0))
      return false;

    // ::ffff:a.b.c.d carries an IPv4 address in its last four bytes
    if (address.IsIPv4MappedToIPv6)
      return IsPublicIPv4(bytes[12..]);

    // link-local fe80::/10, site-local fec0::/10, Teredo and unique-local fc00::/7 stay inside
    // their own network, a Teredo endpoint included (it needs a relay both sides reach)
    return !address.IsIPv6LinkLocal
      && !address.IsIPv6SiteLocal
      && !address.IsIPv6Teredo
      && !IsIPv6UniqueLocal(bytes);
  }

  private static bool IsPublicIPv4(byte[] b)
  {
    // 0.0.0.0, loopback 127/8, private 10/8 and 172.16/12 and 192.168/16, link-local 169.254/16,
    // reserved 240/4, and carrier-grade NAT 100.64/10 (the mesh addresses a VPN gives out,
    // which is exactly the "works on my other device, unreachable for everyone else" case)
    if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] == 169 || b[0] >= 240)
      return false;

    if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
      return false;

    if (b[0] == 192 && b[1] == 168)
      return false;

    return !(b[0] == 100 && b[1] >= 64 && b[1] <= 127);
  }

  private static bool IsIPv6UniqueLocal(byte[] bytes) => (bytes[0] & 0xfe) == 0xfc;
}
