using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Onboarding.GetOnboarding;
using StockIndicators.Application.Onboarding.SetOnboarding;
using StockIndicators.Contracts.Onboarding;

namespace StockIndicators.Controllers;

/// <summary>
/// The settings-page guide's dismissal flag. The copy is frontend i18n; the database only
/// remembers that the operator has seen it, so the guide stays hidden on every device.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OnboardingController : ControllerBase
{
  private readonly IMediator _mediator;

  public OnboardingController(IMediator mediator) => _mediator = mediator;

  [HttpGet]
  public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
    Ok(await _mediator.Send(new GetOnboardingQuery(), cancellationToken));

  [HttpPut]
  public async Task<IActionResult> Put([FromBody] OnboardingDto state, CancellationToken cancellationToken)
  {
    await _mediator.Send(new SetOnboardingCommand(state.Hidden), cancellationToken);
    return NoContent();
  }
}
