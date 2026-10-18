using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Indicators.GetLandingShareImage;

/// <summary>
/// The link-preview picture of the landing page and of the indicator catalog, localized:
/// a real example chart with a real trigger marker, under the brand name and the product
/// pitch. Served anonymously by <c>GET /api/share/landing/image.png</c> as the
/// <c>og:image</c> of <c>/{locale}</c> and <c>/{locale}/indicators</c>.
/// </summary>
public record GetLandingShareImageQuery(string Language) : IQuery<byte[]?>
{
  public static string CacheKey(string indicator, string language) => $"landing-share-image:{indicator}:{language}";
}
