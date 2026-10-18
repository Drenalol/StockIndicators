using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.GetIndicatorShareImage;

/// <summary>
/// The link-preview picture of one indicator documentation page, localized: the example
/// chart of the indicator with its name and description drawn over it. Served anonymously
/// by <c>GET /api/share/indicators/{indicator}/image.png</c>, which is what the
/// <c>og:image</c> of <c>/{locale}/indicators/{name}</c> points at - a messenger crawler
/// has no session, so the picture has to be public, unlike the HMAC-signed push images.
/// </summary>
public record GetIndicatorShareImageQuery(Indicator Indicator, string Language) : IQuery<byte[]?>
{
  public static string CacheKey(Indicator indicator, string language) => $"indicator-share-image:{indicator}:{language}";
}
