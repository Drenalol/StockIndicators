using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Providers.GetProviders;
using StockIndicators.Application.Providers.SaveProviderSettings;
using StockIndicators.Application.Providers.TestProvider;
using StockIndicators.Contracts.Providers;

namespace StockIndicators.Controllers;

/// <summary>
/// The settings page's provider registry: what can be turned on, what is on, and what the
/// operator's keys are (masked). Saving never applies mid-flight - which providers exist is
/// decided while the app composes itself, so the page says "restart to apply".
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
  private readonly IMediator _mediator;

  public ProvidersController(IMediator mediator) => _mediator = mediator;

  [HttpGet]
  public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
    Ok(await _mediator.Send(new GetProvidersQuery(), cancellationToken));

  [HttpPut("{code}")]
  public async Task<IActionResult> Save(string code, [FromBody] SaveProviderRequest request, CancellationToken cancellationToken)
  {
    await _mediator.Send(
      new SaveProviderSettingsCommand(
        code,
        request.IsEnabled,
        request.Credentials ?? [],
        request.ClearFields ?? []),
      cancellationToken);

    return Ok(new { restartRequired = true });
  }

  [HttpPost("{code}/test")]
  public async Task<IActionResult> Test(string code, [FromBody] TestProviderRequest? request, CancellationToken cancellationToken) =>
    Ok(await _mediator.Send(new TestProviderCommand(code, request?.Credentials), cancellationToken));
}
