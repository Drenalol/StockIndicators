using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators.GetIndicatorShareImage;
using StockIndicators.Application.Indicators.GetLandingShareImage;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.PublicSite;

namespace StockIndicators.Controllers;

/// <summary>
/// Public pictures for links shared outside the app. A messenger crawler (Telegram,
/// WhatsApp, Discord, VK) fetches the shared URL with no session and reads the
/// <c>og:image</c> out of the HTML, so the picture a link previews with cannot be the
/// HMAC-signed push image - it has to be an anonymous endpoint whose URL the static page
/// can hardcode. Only indicator documentation is pictured: everything under /dashboard is
/// the visitor's own data and is closed to crawlers by robots.txt.
/// </summary>
[Route("api/share")]
[ApiController]
[AllowAnonymous]
[PublicSite]
public class ShareController : ControllerBase
{
  /// <summary>
  /// The static brand card the web root serves (the same picture the landing and the
  /// indicator catalog point their <c>og:image</c> at). A link is never left without a
  /// picture: when the chart cannot be rendered the endpoint redirects here.
  /// </summary>
  private const string BrandCardPath = "/og/brand.png";

  /// <summary>
  /// How long the picture may be cached. Long by design (Cloudflare's edge keeps a
  /// <c>.png</c> this long, so a link shared into a thousand chats costs one render), and
  /// it costs nothing: the card is deterministic per app version.
  /// </summary>
  private static readonly TimeSpan ImageCacheDuration = TimeSpan.FromDays(7);

  private readonly IMediator _mediator;
  private readonly IOptions<AlertOptions> _alertOptions;

  public ShareController(IMediator mediator, IOptions<AlertOptions> alertOptions)
  {
    _mediator = mediator;
    _alertOptions = alertOptions;
  }

  /// <summary>
  /// The link-preview card of one indicator documentation page (<c>lang</c>: any supported
  /// language, unknown ones fall back to English). Answers the brand card when the picture
  /// cannot be produced at all, so an unfurl never shows a text-only preview.
  /// </summary>
  [HttpGet("indicators/{indicator}/image.png")]
  public async Task<IActionResult> GetIndicatorImageAsync(
    Indicator indicator,
    [FromQuery] string? lang,
    CancellationToken cancellationToken)
  {
    var png = await _mediator.Send(
      new GetIndicatorShareImageQuery(indicator, UserLocale.NormalizeLanguage(lang)),
      cancellationToken);

    if (png is not { Length: > 0 })
      return BrandCard();

    Response.Headers.CacheControl = $"public, max-age={(int)ImageCacheDuration.TotalSeconds}";

    return File(png, TriggerChartImages.PngMimeType);
  }

  /// <summary>
  /// The link-preview card of the landing page and of the indicator catalog (<c>lang</c>:
  /// any supported language, unknown ones fall back to English): a real example chart with
  /// its trigger marker under the brand name and the product pitch. The indicator it shows
  /// is the deployment's choice (<c>ShareImage:LandingIndicator</c>), not the caller's -
  /// an anonymous, long-cached picture must not be a way to mint arbitrary text or arbitrary
  /// chart parameters into a public PNG on our own domain.
  /// </summary>
  [HttpGet("landing/image.png")]
  public async Task<IActionResult> GetLandingImageAsync(
    [FromQuery] string? lang,
    CancellationToken cancellationToken)
  {
    var png = await _mediator.Send(new GetLandingShareImageQuery(UserLocale.NormalizeLanguage(lang)), cancellationToken);

    if (png is not { Length: > 0 })
      return BrandCard();

    Response.Headers.CacheControl = $"public, max-age={(int)ImageCacheDuration.TotalSeconds}";

    return File(png, TriggerChartImages.PngMimeType);
  }

  private IActionResult BrandCard()
  {
    // The brand card is a file this very origin serves, so its URL is built from the request
    // (the forwarded headers are trusted whenever a proxy stands in front): no deployment has
    // to configure anything to get a link preview. AlertOptions:PublicBaseUrl wins when it is
    // set - notifications already use it - but it is a notification setting, and a public-site
    // instance has no notifications to configure, so it must not be what decides whether a
    // shared link has a picture at all.
    var configured = _alertOptions.Value.PublicBaseUrl;

    var baseUrl = string.IsNullOrWhiteSpace(configured)
      ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"
      : configured;

    // Never cached: this is a degradation, and caching it would keep serving the brand card
    // after the renderer is healthy again
    Response.Headers.CacheControl = "no-store";

    return Redirect($"{baseUrl.TrimEnd('/')}{BrandCardPath}");
  }
}
