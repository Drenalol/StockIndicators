using System.Security.Cryptography;
using System.Text;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Cache keys, TTLs and HMAC signature helpers for publicly served trigger chart images.
/// The signature binds the trigger id to an expiry timestamp, so signed URLs are
/// short-lived and unforgeable without the shared <c>ImageSigningKey</c>.
/// </summary>
public static class TriggerChartImages
{
  public const string PngMimeType = "image/png";

  public static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);
  public static readonly TimeSpan SignatureTtl = TimeSpan.FromHours(24);

  public static string CacheKey(Guid triggerId) => $"trigger-chart-{triggerId:N}";

  public static string ComputeSignature(string signingKey, Guid triggerId, long expUnix)
  {
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
    return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(BuildSignedPayload(triggerId, expUnix))));
  }

  /// <summary>
  /// Verifies a signed image URL. The signature must match and the expiry must be in the
  /// future but not absurdly far ahead (guards against clock-skew replay of old URLs).
  /// </summary>
  public static bool VerifySignature(string signingKey, Guid triggerId, long expUnix, string signature)
  {
    if (string.IsNullOrWhiteSpace(signingKey) || string.IsNullOrWhiteSpace(signature))
      return false;

    var now = DateTimeOffset.UtcNow;

    if (expUnix <= now.ToUnixTimeSeconds() || expUnix > now.AddYears(1).ToUnixTimeSeconds())
      return false;

    var expected = ComputeSignature(signingKey, triggerId, expUnix);
    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
  }

  public static string BuildSignedUrl(string publicBaseUrl, string signingKey, Guid triggerId)
  {
    var exp = DateTimeOffset.UtcNow.Add(SignatureTtl).ToUnixTimeSeconds();
    var signature = ComputeSignature(signingKey, triggerId, exp);

    return $"{publicBaseUrl.TrimEnd('/')}/api/images/triggers/{triggerId:N}?exp={exp}&sig={Uri.EscapeDataString(signature)}";
  }

  private static string BuildSignedPayload(Guid triggerId, long expUnix) => $"{triggerId:N}|{expUnix}";
}
