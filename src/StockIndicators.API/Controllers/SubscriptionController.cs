using System.ComponentModel.DataAnnotations;
using MediatR;
using SubscriptionDto = StockIndicators.Application.Subscriptions.GetSubscription.SubscriptionDto;
using SubscriptionSnapshotDto = StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot.SubscriptionSnapshotDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using StockIndicators.Application.Subscriptions.DeleteSubscription;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Application.Subscriptions.GetSubscriptions;
using StockIndicators.Application.Subscriptions.GetTriggerHistory;
using StockIndicators.Application.Subscriptions.GetTriggerChartData;
using StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;
using StockIndicators.Application.Subscriptions.GetSnapshotChartData;
using StockIndicators.Application.Subscriptions.RefreshSubscriptionSnapshot;
using StockIndicators.Application.Subscriptions.PatchSubscription;
using StockIndicators.Application.Subscriptions.SetSubscriptionActive;
using StockIndicators.Application.Subscriptions.MarkSubscriptionRead;
using StockIndicators.Application.Subscriptions.MarkAllSubscriptionsRead;
using StockIndicators.Contracts.Subscriptions;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SubscriptionController : ControllerBase
{
  private readonly IMediator _mediator;

  public SubscriptionController(IMediator mediator) => _mediator = mediator;

  // Chip filters of the list page (URL state): q - instrument text, status (repeatable),
  // type/exchange/indicator/rule=flat|multi, triggered - alerts within the last week.
  [HttpGet]
  public async Task<ActionResult<IEnumerable<SubscriptionDto>>> GetAllAsync(
    [FromQuery] string? q,
    [FromQuery] string[]? status,
    [FromQuery] string? type,
    [FromQuery] string? indicator,
    [FromQuery] string? exchange,
    [FromQuery] string? rule,
    [FromQuery] bool? triggered,
    CancellationToken cancellationToken)
  {
    var filters = new GetSubscriptionsFilters
    {
      Search = q,
      Statuses = status is { Length: > 0 } ? status : null,
      Type = type,
      Indicator = indicator,
      ExchangeName = exchange,
      RuleType = rule,
      TriggeredRecently = triggered,
    };

    return await _mediator.Send(new GetSubscriptionsQuery(HttpContext.GetUserId(), filters), cancellationToken);
  }

  [HttpGet("{subscriptionId:guid}")]
  public async Task<ActionResult<SubscriptionDto>> GetByIdAsync([Required] Guid subscriptionId, CancellationToken cancellationToken) => await _mediator.Send(new GetSubscriptionQuery(subscriptionId, HttpContext.GetUserId()), cancellationToken);

  [HttpGet("{subscriptionId:guid}/trigger-history")]
  public async Task<ActionResult<IEnumerable<TriggerHistoryDto>>> GetTriggerHistoryAsync([Required] Guid subscriptionId, CancellationToken cancellationToken)
  {
    var result = await _mediator.Send(new GetTriggerHistoryQuery(subscriptionId, HttpContext.GetUserId()), cancellationToken);
    return Ok(result);
  }

  // Clears the "new alert" flag (the list's New badge): stamps LastReadAt so an alert counts
  // as new again only when a newer notification arrives. Idempotent.
  [HttpPost("{subscriptionId:guid}/read")]
  public async Task<ActionResult> MarkReadAsync([Required] Guid subscriptionId, CancellationToken cancellationToken)
  {
    await _mediator.Send(new MarkSubscriptionReadCommand(subscriptionId, HttpContext.GetUserId()), cancellationToken);

    return NoContent();
  }

  // Clears every "new alert" flag of the user at once (the list's "Mark all as read" button):
  // one bulk UPDATE over the unread subscriptions. Idempotent.
  [HttpPost("read-all")]
  public async Task<ActionResult> MarkAllReadAsync(CancellationToken cancellationToken)
  {
    await _mediator.Send(new MarkAllSubscriptionsReadCommand(HttpContext.GetUserId()), cancellationToken);

    return NoContent();
  }

  // The trigger chart as raw series data for the client chart core (candles, overlays,
  // volume, trigger marker). Alert channels use the PNG variant (GetTriggerChartImageQuery).
  [HttpGet("{subscriptionId:guid}/triggers/{triggerId:guid}/chart")]
  public async Task<ActionResult> GetTriggerChartAsync([Required] Guid subscriptionId, [Required] Guid triggerId, CancellationToken cancellationToken)
  {
    var data = await _mediator.Send(new GetTriggerChartDataQuery(subscriptionId, triggerId, HttpContext.GetUserId()), cancellationToken);

    return data is null ? NotFound() : Ok(data);
  }

  // The live chart snapshot of the subscription: one chart per (instrument, timeframe) group
  // of its indicator conditions, cached in the DB (TriggerSnapshots). A cache-miss GET renders
  // once for free; the "Refresh" button below is the quota-limited part. 404 when the candle
  // cache window expired for every instrument.
  [HttpGet("{subscriptionId:guid}/snapshot")]
  public async Task<ActionResult<SubscriptionSnapshotDto>> GetSnapshotAsync([Required] Guid subscriptionId, CancellationToken cancellationToken)
  {
    var dto = await _mediator.Send(new GetSubscriptionSnapshotQuery(subscriptionId, HttpContext.GetUserId()), cancellationToken);
    return Ok(dto);
  }

  // Re-renders the snapshot from the current rule and updates the stored charts. Daily quota
  // per subscription (free: 50, premium currently unlimited, UTC day); 429 when exhausted.
  [HttpPost("{subscriptionId:guid}/snapshot/refresh")]
  public async Task<ActionResult<SubscriptionSnapshotDto>> RefreshSnapshotAsync([Required] Guid subscriptionId, CancellationToken cancellationToken)
  {
    var dto = await _mediator.Send(new RefreshSubscriptionSnapshotCommand(subscriptionId, HttpContext.GetUserId()), cancellationToken);
    return Ok(dto);
  }

  // The snapshot chart as raw series data for the client chart core, computed from the
  // live candle cache (the stored row only identifies the instrument/timeframe group).
  [HttpGet("{subscriptionId:guid}/snapshot/{snapshotId:guid}/chart")]
  public async Task<ActionResult> GetSnapshotChartAsync([Required] Guid subscriptionId, [Required] Guid snapshotId, CancellationToken cancellationToken)
  {
    var data = await _mediator.Send(new GetSnapshotChartDataQuery(subscriptionId, snapshotId, HttpContext.GetUserId()), cancellationToken);

    return data is null ? NotFound() : Ok(data);
  }

  [HttpPost("{subscriptionId:guid}/active")]
  public async Task<ActionResult> SetActiveAsync([Required] Guid subscriptionId, [Required, FromBody] SetSubscriptionActiveRequest request, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();
    await _mediator.Send(new SetSubscriptionActiveCommand(subscriptionId, userId, request.IsActive), cancellationToken);

    return NoContent();
  }

  [HttpPatch("{subscriptionId:guid}")]
  public async Task<ActionResult> PartialUpdateAsync([Required] Guid subscriptionId, [Required, FromBody] JsonPatchDocument patchDocument, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();
    await _mediator.Send(new PatchSubscriptionCommand(subscriptionId, userId, patchDocument), cancellationToken);

    return NoContent();
  }

  [HttpDelete("{subscriptionId:guid}")]
  public async Task<ActionResult> DeleteAsync([Required] Guid subscriptionId, CancellationToken cancellationToken)
  {
    await _mediator.Send(new DeleteSubscriptionCommand(subscriptionId, HttpContext.GetUserId()), cancellationToken);

    return NoContent();
  }
}
