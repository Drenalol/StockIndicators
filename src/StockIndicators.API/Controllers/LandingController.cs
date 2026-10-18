using MediatR;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators.GetLandingExamples;
using StockIndicators.PublicSite;

namespace StockIndicators.Controllers;

/// <summary>
/// Public landing page content. Deliberately anonymous (no <c>[Authorize]</c>): the landing
/// is the entry page for unauthenticated users.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[PublicSite]
public class LandingController : ControllerBase
{
  private readonly IMediator _mediator;

  public LandingController(IMediator mediator) => _mediator = mediator;

  /// <summary>
  /// The real-chart gallery for the landing page: example trigger charts of several indicators
  /// rendered from the embedded candle fixtures as interactive series data.
  /// <paramref name="lang"/>: en (default), ru or es - it only localizes the trigger label.
  /// </summary>
  [HttpGet("examples")]
  public async Task<ActionResult<LandingExampleChartDto[]>> GetExamplesAsync(
    [FromQuery] string? lang,
    CancellationToken cancellationToken)
  {
    var language = UserLocale.NormalizeLanguage(lang);

    return Ok(await _mediator.Send(new GetLandingExamplesQuery(language), cancellationToken));
  }
}
