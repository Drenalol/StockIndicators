using Microsoft.Extensions.Options;

namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// Second cascade source: the Logo.dev image CDN. Crypto symbols are looked up directly
/// (<c>/crypto/BTC</c>); stocks/ETFs first by ticker (US exchanges resolve without a
/// suffix), then by ISIN. <c>fallback=404</c> is required: without it Logo.dev answers
/// 200 with a generated monogram image, which must not be stored as "the logo" - the UI
/// renders its own monogram fallback instead.
/// The free plan bills CDN requests (500k/month): every logo is fetched once and cached
/// in Redis, and the publishable key only ever leaves the server into download URLs.
/// </summary>
public class LogoDevLogoSource : IInstrumentLogoSource
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly LogoOptions _options;

  public LogoDevLogoSource(IHttpClientFactory httpClientFactory, IOptions<LogoOptions> options)
  {
    _httpClientFactory = httpClientFactory;
    _options = options.Value;
  }

  public async Task<InstrumentLogoData?> TryResolveAsync(InstrumentLogoRequest request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(_options.LogoDevPublishableKey))
      return null;

    var http = _httpClientFactory.CreateClient(LogoHttpClients.Client);

    foreach (var path in LookupPaths(request))
    {
      var data = await LogoDownloads.DownloadAsync(http, BuildUrl(_options, path), "logo.dev", cancellationToken);

      if (data is not null)
        return data;
    }

    return null;
  }

  public static IReadOnlyList<string> LookupPaths(InstrumentLogoRequest request)
  {
    if (request.IsCrypto)
      return [$"crypto/{Uri.EscapeDataString(request.BaseAsset)}"];

    var paths = new List<string> { $"ticker/{Uri.EscapeDataString(request.Ticker)}" };

    if (!string.IsNullOrWhiteSpace(request.Catalog?.Isin))
      paths.Add($"isin/{Uri.EscapeDataString(request.Catalog.Isin)}");

    return paths;
  }

  public static Uri BuildUrl(LogoOptions options, string path) =>
    new($"{options.LogoDevBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(options.LogoDevPublishableKey)}&format=png&size=64&fallback=404");
}
