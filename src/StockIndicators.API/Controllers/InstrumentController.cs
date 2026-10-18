using System.Security.Cryptography;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Instruments.GetInstrument;
using StockIndicators.Application.Instruments.GetInstrumentChart;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Instruments.GetInstrumentLogo;
using StockIndicators.Application.Subscriptions.CreateSubscription;
using StockIndicators.Application.Subscriptions.DeleteSubscriptionsByInstrument;
using ApplicationOutputRequest = StockIndicators.Application.Subscriptions.CreateSubscription.SubscriptionOutputRequest;
using StockIndicators.Infrastructure.Processing;

namespace StockIndicators.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InstrumentController : ControllerBase
{
  private readonly IMediator _mediator;

  public InstrumentController(IMediator mediator) => _mediator = mediator;

  [HttpGet]
  public async Task<ActionResult<PaginatedResult<InstrumentDto>>> GetAllAsync(
    [FromQuery] string? search,
    [FromQuery] string? type,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    [FromQuery] string? indicator = null,
    [FromQuery] string? exchange = null,
    CancellationToken cancellationToken = default)
  {
    var query = new GetInstrumentsQuery(search, type, page, pageSize, indicator, exchange, HttpContext.GetUserId());
    var result = await _mediator.Send(query, cancellationToken);
    return Ok(result);
  }

  // Public per-instrument logo (cached in Redis, resolved from Tinkoff brand data / Logo.dev /
  // CoinGecko). Anonymous because <img> tags carry no bearer token; the image itself is not
  // secret. 404 means "no source had a logo" and the UI renders its own letter monogram.
  [AllowAnonymous]
  [HttpGet("logo")]
  public async Task<IActionResult> GetLogoAsync([FromQuery] string ticker, [FromQuery] string? exchange, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(ticker) || ticker.Length > 64)
      return NotFound();

    var logo = await _mediator.Send(new GetInstrumentLogoQuery(ticker, exchange), cancellationToken);

    if (logo is null)
      return NotFound();

    var etag = $"\"{Convert.ToHexString(SHA256.HashData(logo.Data).AsSpan()[..12])}\"";

    Response.Headers.ETag = etag;
    Response.Headers.CacheControl = "public, max-age=86400";

    if (Request.Headers.IfNoneMatch == etag)
      return StatusCode(StatusCodes.Status304NotModified);

    return File(logo.Data, logo.ContentType);
  }

  [HttpGet("{ticker}")]
  public async Task<ActionResult<InstrumentDetailDto>> GetAsync(string ticker, [FromQuery] string? exchange, CancellationToken cancellationToken)
  {
    var detail = await _mediator.Send(new GetInstrumentQuery(ticker, exchange), cancellationToken);
    return Ok(detail);
  }

  // Plain price preview of an instrument (daily candles, no indicators): the same ChartData
  // payload the client chart core draws elsewhere. 404 when the cache holds no candles for
  // the instrument (the client shows no chart instead of an error).
  [HttpGet("{ticker}/chart")]
  public async Task<ActionResult<ChartData>> GetChartAsync(string ticker, [FromQuery] string? exchange, CancellationToken cancellationToken)
  {
    var data = await _mediator.Send(new GetInstrumentChartQuery(ticker, exchange, HttpContext.GetUserId()), cancellationToken);

    return data is null ? NotFound() : Ok(data);
  }

  [HttpPost("{ticker}/subscription")]
  public async Task<ActionResult<Guid>> CreateAsync(string ticker, [FromQuery] string exchangeName, [FromBody] SubscriptionCreateRequest request, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();
    var outputs = request.Outputs?.Select(o => new ApplicationOutputRequest(o.Channel, o.NotificationTemplateId, o.PushSubscriptionId, o.ChatId, o.Target)).ToList();

    InstrumentIdentity? instrument = null;

    if (request.Instrument is not null)
    {
      // A client identity whose exchange disagrees with the requested one is a stale or
      // forged selection: reject it as NotFound instead of falling through to providers.
      if (!string.Equals(request.Instrument.ExchangeName ?? "", exchangeName ?? "", StringComparison.OrdinalIgnoreCase))
        return NotFound(new { error = $"Instrument '{ticker}' is not available on exchange '{exchangeName}'" });

      instrument = new InstrumentIdentity(request.Instrument.Source, request.Instrument.ExchangeName);
    }

    var subscriptionId = await _mediator.Send(new CreateSubscriptionCommand(userId, ticker, exchangeName, TimeSpan.FromSeconds(request.ResolvedCooldownSeconds), request.Rule, outputs, instrument, request.IgnoreExtendedSessions ?? true), cancellationToken);
    return Created(string.Empty, subscriptionId);
  }

  // Deletes all current user subscriptions for the instrument in one operation.
  // ExchangeName is required in the body: null matches only subscriptions without an exchange.
  [HttpDelete("{ticker}/subscriptions")]
  public async Task<ActionResult> DeleteSubscriptionsAsync(string ticker, [FromBody] DeleteSubscriptionsByInstrumentRequest request, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();
    await _mediator.Send(new DeleteSubscriptionsByInstrumentCommand(userId, ticker, request.ExchangeName), cancellationToken);
    return NoContent();
  }
}
