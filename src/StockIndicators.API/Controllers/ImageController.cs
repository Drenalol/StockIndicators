using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Subscriptions.GetTriggerChart;

namespace StockIndicators.Controllers;

/// <summary>
/// Public trigger chart images for web push notifications. There is no token in a push
/// payload, so access is controlled by a short-lived HMAC signature on the URL.
/// </summary>
[Route("api/images/triggers")]
[ApiController]
public class ImageController : ControllerBase
{
  private readonly IMediator _mediator;
  private readonly IOptions<AlertOptions> _alertOptions;

  public ImageController(IMediator mediator, IOptions<AlertOptions> alertOptions)
  {
    _mediator = mediator;
    _alertOptions = alertOptions;
  }

  [HttpGet("{triggerId:guid}")]
  public async Task<IActionResult> GetTriggerChartAsync(
    Guid triggerId,
    [FromQuery] long? exp,
    [FromQuery] string? sig,
    CancellationToken cancellationToken
  )
  {
    if (!TriggerChartImages.VerifySignature(_alertOptions.Value.ImageSigningKey, triggerId, exp ?? 0, sig ?? string.Empty))
      return Unauthorized();

    var png = await _mediator.Send(new GetTriggerChartImageQuery(triggerId), cancellationToken);

    if (png is null)
      return NotFound();

    return File(png, TriggerChartImages.PngMimeType);
  }
}
