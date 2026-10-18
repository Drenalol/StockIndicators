using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules.GetRulePresets;
using StockIndicators.PublicSite;

namespace StockIndicators.Controllers;

// Public (anonymous): the built-in preset catalog contains no user data and also
// feeds the public landing indicators pages alongside the dashboard.
[Route("api/rule-preset")]
[ApiController]
[AllowAnonymous]
[PublicSite]
public class RulePresetController : ControllerBase
{
  private readonly IMediator _mediator;

  public RulePresetController(IMediator mediator) => _mediator = mediator;

  /// <summary>
  /// The built-in one-tap rule presets localized into <paramref name="lang"/>
  /// (any interface language, English for one the catalog does not carry): ready-made
  /// condition trees that pre-fill the rule constructor.
  /// </summary>
  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<RulePresetDto>>> GetAsync(
    [FromQuery] string? lang = null,
    CancellationToken cancellationToken = default) =>
    Ok(await _mediator.Send(new GetRulePresetsQuery(UserLocale.NormalizeLanguage(lang)), cancellationToken));
}
