using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using StockIndicators.Application.Notifications.CreateNotificationTemplate;
using StockIndicators.Application.Notifications.DeleteNotificationTemplate;
using StockIndicators.Application.Notifications.GetNotificationTemplates;
using StockIndicators.Application.Notifications.UpdateNotificationTemplate;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

[Route("api/notification-template")]
[ApiController]
[Authorize]
public class NotificationTemplateController : ControllerBase
{
  private readonly IMediator _mediator;

  public NotificationTemplateController(IMediator mediator) => _mediator = mediator;

  [HttpGet]
  public async Task<ActionResult<List<NotificationTemplateDto>>> GetAllAsync(CancellationToken cancellationToken) =>
    Ok(await _mediator.Send(new GetNotificationTemplatesQuery(HttpContext.GetUserId()), cancellationToken));

  [HttpPost]
  public async Task<ActionResult<Guid>> CreateAsync([FromBody] CreateNotificationTemplateRequest request, CancellationToken cancellationToken)
  {
    var id = await _mediator.Send(new CreateNotificationTemplateCommand(HttpContext.GetUserId(), request.Name, request.Template), cancellationToken);
    return Created(string.Empty, id);
  }

  [HttpPut("{id:guid}")]
  public async Task<ActionResult> UpdateAsync(Guid id, [FromBody] UpdateNotificationTemplateRequest request, CancellationToken cancellationToken)
  {
    await _mediator.Send(new UpdateNotificationTemplateCommand(HttpContext.GetUserId(), id, request.Name, request.Template), cancellationToken);
    return NoContent();
  }

  [HttpDelete("{id:guid}")]
  public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
  {
    await _mediator.Send(new DeleteNotificationTemplateCommand(HttpContext.GetUserId(), id), cancellationToken);
    return NoContent();
  }
}

public class CreateNotificationTemplateRequest
{
  [System.ComponentModel.DataAnnotations.Required]
  public string Name { get; set; } = null!;

  [System.ComponentModel.DataAnnotations.Required]
  public string Template { get; set; } = null!;
}

public class UpdateNotificationTemplateRequest
{
  [System.ComponentModel.DataAnnotations.Required]
  public string Name { get; set; } = null!;

  [System.ComponentModel.DataAnnotations.Required]
  public string Template { get; set; } = null!;
}
