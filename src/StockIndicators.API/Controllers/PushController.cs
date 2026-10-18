using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Infrastructure.Processing;
using WebPush;
using DomainPushSubscription = StockIndicators.Domain.Models.PushSubscriptions.PushSubscription;

namespace StockIndicators.Controllers;

[Route("api/push")]
[ApiController]
[Authorize]
public class PushController : ControllerBase
{
  private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
  private readonly IOptions<WebPushOptions> _webPushOptions;
  private readonly WebPushClient _webPushClient;

  public PushController(
    IPushSubscriptionRepository pushSubscriptionRepository,
    IOptions<WebPushOptions> webPushOptions,
    WebPushClient webPushClient
  )
  {
    _pushSubscriptionRepository = pushSubscriptionRepository;
    _webPushOptions = webPushOptions;
    _webPushClient = webPushClient;
  }

  [HttpGet("vapid-public-key")]
  public ActionResult<object> GetVapidPublicKey()
  {
    if (string.IsNullOrEmpty(_webPushOptions.Value.VapidPublicKey))
      return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Web Push VAPID is not configured" });

    return Ok(new { key = _webPushOptions.Value.VapidPublicKey });
  }

  [HttpPost("subscriptions")]
  public async Task<ActionResult<object>> SubscribeAsync([FromBody] PushSubscriptionRequest request, CancellationToken cancellationToken)
  {
    var userId = HttpContext.GetUserId();

    // Pre-read only picks the response status (201 new / 200 refresh); correctness is guaranteed by the upsert
    var isNew = (await _pushSubscriptionRepository.GetByUserAsync(userId, cancellationToken)).All(p => p.Endpoint != request.Endpoint);

    var pushSubscription = new DomainPushSubscription
    {
      Id = Guid.CreateVersion7(),
      UserId = userId,
      Platform = PushPlatform.Web,
      Endpoint = request.Endpoint,
      PublicKey = request.Keys?.P256dh,
      AuthKey = request.Keys?.Auth,
      DeviceLabel = request.DeviceLabel,
      Created = DateTime.UtcNow
    };

    var saved = await _pushSubscriptionRepository.UpsertAsync(pushSubscription, cancellationToken);

    return isNew ? Created(string.Empty, ToDto(saved)) : Ok(ToDto(saved));
  }

  [HttpGet("subscriptions")]
  public async Task<ActionResult<List<object>>> GetSubscriptionsAsync(CancellationToken cancellationToken)
  {
    var subscriptions = await _pushSubscriptionRepository.GetByUserAsync(HttpContext.GetUserId(), cancellationToken);
    return Ok(subscriptions.Select(ToDto).ToList());
  }

  [HttpDelete("subscriptions/{subscriptionId:guid}")]
  public async Task<ActionResult> UnsubscribeAsync(Guid subscriptionId, CancellationToken cancellationToken)
  {
    await _pushSubscriptionRepository.DeleteAsync(subscriptionId, HttpContext.GetUserId(), cancellationToken);
    await _pushSubscriptionRepository.CommitAsync(cancellationToken);
    return NoContent();
  }

  /// <summary>Sends a test notification to all Web push subscriptions of the user.</summary>
  [HttpPost("test")]
  public async Task<ActionResult<object>> SendTestAsync(CancellationToken cancellationToken)
  {
    var vapid = _webPushOptions.Value;

    if (string.IsNullOrEmpty(vapid.VapidPrivateKey) || string.IsNullOrEmpty(vapid.VapidPublicKey) || string.IsNullOrEmpty(vapid.Subject))
      return BadRequest(new { error = "Web Push VAPID is not configured" });

    var subscriptions = await _pushSubscriptionRepository.GetByUserAsync(HttpContext.GetUserId(), cancellationToken);
    var vapidDetails = new VapidDetails(vapid.Subject, vapid.VapidPublicKey, vapid.VapidPrivateKey);
    var payload = JsonSerializer.Serialize(new { title = "Stock Indicators", body = "Test notification", icon = "/icons/icon-192.png", data = new { path = "/dashboard/subscriptions" } });
    var sent = 0;

    foreach (var pushSubscription in subscriptions.Where(p => p.Platform == PushPlatform.Web))
    {
      try
      {
        await _webPushClient.SendNotificationAsync(
          new WebPush.PushSubscription(pushSubscription.Endpoint, pushSubscription.PublicKey, pushSubscription.AuthKey),
          payload,
          vapidDetails,
          cancellationToken);
        sent++;
      }
      catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
      {
        await _pushSubscriptionRepository.DeleteAsync(pushSubscription.Id, HttpContext.GetUserId(), cancellationToken);
        await _pushSubscriptionRepository.CommitAsync(cancellationToken);
      }
      catch (WebPushException)
      {
        // keep testing the remaining subscriptions
      }
    }

    return Ok(new { sent });
  }

  private static object ToDto(DomainPushSubscription pushSubscription) => new
  {
    id = pushSubscription.Id,
    platform = pushSubscription.Platform.ToString(),
    endpoint = pushSubscription.Endpoint,
    deviceLabel = pushSubscription.DeviceLabel,
    created = pushSubscription.Created
  };
}

public record PushSubscriptionRequest(string Endpoint, PushSubscriptionKeysRequest? Keys, string? DeviceLabel = null);
public record PushSubscriptionKeysRequest(string? P256dh, string? Auth);
