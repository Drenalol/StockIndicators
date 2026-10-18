using System.Security.Cryptography;
using Serilog;
using StockIndicators.Application.Providers;

namespace StockIndicators.Internal;

/// <summary>
/// Web Push keys the operator should never have to generate: an ECDSA P-256 pair in the
/// format the Push API expects (uncompressed public point, raw private scalar, base64url).
/// Created once after migrations, stored like any other provider's credentials, visible on
/// the settings page.
/// </summary>
/// <summary>The base64url alphabet the Web Push and Telegram token worlds share.</summary>
public static class Base64Url
{
  public static string Encode(byte[] data) =>
    Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

  public static byte[] Decode(string value) =>
    Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}

public static class WebPushKeys
{
  public static (string PublicKey, string PrivateKey) Generate()
  {
    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    var parameters = ecdsa.ExportParameters(includePrivateParameters: true);

    var publicKey = new byte[65];
    publicKey[0] = 0x04;
    Buffer.BlockCopy(parameters.Q.X!, 0, publicKey, 1, 32);
    Buffer.BlockCopy(parameters.Q.Y!, 0, publicKey, 33, 32);

    return (Base64Url.Encode(publicKey), Base64Url.Encode(parameters.D!));
  }
}

public static class WebPushKeysInitializer
{
  /// <summary>
  /// Ensures the webpush row carries a VAPID pair. Runs right after migrations, before any
  /// request can resolve the options - so the frontend's push subscription dialog always
  /// finds a public key to subscribe with.
  /// </summary>
  public static async Task EnsureWebPushKeysAsync(this IApplicationBuilder app)
  {
    using var scope = app.ApplicationServices.CreateScope();

    var store = scope.ServiceProvider.GetRequiredService<IProviderSettingsStore>();

    var rows = await store.LoadAllAsync();

    rows.TryGetValue("webpush", out var row);

    if (row is not null &&
        row.Credentials.TryGetValue("VapidPublicKey", out var existingPublic) && existingPublic.Length > 0 &&
        row.Credentials.TryGetValue("VapidPrivateKey", out var existingPrivate) && existingPrivate.Length > 0)
      return;

    var (publicKey, privateKey) = WebPushKeys.Generate();

    var credentials = new Dictionary<string, string>(row?.Credentials ?? new Dictionary<string, string>())
    {
      ["VapidPublicKey"] = publicKey,
      ["VapidPrivateKey"] = privateKey,
    };

    // the push services want a contact; nobody reads it in practice, but the spec does
    if (!credentials.ContainsKey("Subject"))
      credentials["Subject"] = "mailto:admin@localhost";

    await store.SaveAsync("webpush", row?.IsEnabled ?? true, credentials);

    Log.Information("Generated a VAPID key pair for Web Push (stored in ProviderSettings)");
  }
}
