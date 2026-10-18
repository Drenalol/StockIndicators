using Microsoft.Extensions.Options;

namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// First cascade source: the issuer brand CDN the Invest API's brand data points at.
/// The file name comes from the provider catalog (<see cref="IInstrumentBrandLogoDirectory"/>),
/// the bytes from Tinkoff's public brand CDN. The CDN hosts several naming variants of the
/// same brand (<c>name.png</c>, <c>namex160.png</c>, <c>namex640.png</c>) and answers AccessDenied
/// for the absent ones, so the candidates are tried in order; the small 160px variant is enough
/// for the UI and is preferred over 640px.
/// </summary>
public class TinkoffBrandLogoSource : IInstrumentLogoSource
{
  private readonly List<IInstrumentBrandLogoDirectory> _directories;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly LogoOptions _options;

  public TinkoffBrandLogoSource(
    IEnumerable<IInstrumentBrandLogoDirectory> directories,
    IHttpClientFactory httpClientFactory,
    IOptions<LogoOptions> options)
  {
    _directories = directories.ToList();
    _httpClientFactory = httpClientFactory;
    _options = options.Value;
  }

  public async Task<InstrumentLogoData?> TryResolveAsync(InstrumentLogoRequest request, CancellationToken cancellationToken)
  {
    if (request.IsCrypto)
      return null;

    string? logoName = null;

    foreach (var directory in _directories)
    {
      logoName = await directory.TryGetBrandLogoNameAsync(request.Ticker, cancellationToken);

      if (!string.IsNullOrWhiteSpace(logoName))
        break;
    }

    if (string.IsNullOrWhiteSpace(logoName))
      return null;

    var http = _httpClientFactory.CreateClient(LogoHttpClients.Client);

    foreach (var candidate in BrandFileCandidates(logoName))
    {
      var data = await LogoDownloads.DownloadAsync(http, new Uri(new Uri(_options.TinkoffBrandBaseUrl.TrimEnd('/') + "/"), candidate), "tinkoff", cancellationToken);

      if (data is not null)
        return data;
    }

    return null;
  }

  /// <summary>
  /// File-name variants for a brand: the exact name first ("vkcx640.png" is itself a public object),
  /// then the 160/640 sized variants of the base name ("US78442P1066.png" - "US78442P1066x160.png").
  /// </summary>
  public static IReadOnlyList<string> BrandFileCandidates(string logoName)
  {
    var trimmed = logoName.Trim();
    var withoutExtension = trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;

    return
    [
      trimmed,
      withoutExtension + "x160.png",
      withoutExtension + "x640.png",
      withoutExtension + "640.png",
      withoutExtension + "160.png"
    ];
  }
}
