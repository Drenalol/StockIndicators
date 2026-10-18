using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockIndicators.Application.PublicSite;
using StockIndicators.PublicSite;

namespace StockIndicators.Controllers;

/// <summary>
/// What the frontend needs to know about how this instance is published, before anyone
/// authenticates - on a public-site instance there is nobody to authenticate.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[PublicSite]
public class SiteController : ControllerBase
{
  private readonly IOptions<PublicSiteOptions> _publicSite;

  public SiteController(IOptions<PublicSiteOptions> publicSite) => _publicSite = publicSite;

  /// <summary>
  /// The instance mode. <c>landingOnly</c> tells the frontend to keep the app out of the way:
  /// no sign-in or dashboard entry points, and the landing's calls to action point at the
  /// installation instead. A self-hosted instance answers false and shows the whole product.
  /// </summary>
  [HttpGet("config")]
  public ActionResult<SiteConfigDto> Config() => Ok(new SiteConfigDto(_publicSite.Value.LandingOnly));
}

public record SiteConfigDto(bool LandingOnly);
