using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.PushSubscriptions;
using WebPush;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Web Push channel sender. One notification per alert item, VAPID-encrypted payload.
/// Expired endpoints (404/410 from the push service) remove the push subscription; 429 stops the batch.
/// Every item (including the ones the batch stopped on) gets an explicit delivery outcome.
/// </summary>
public class WebPushAlertChannelSender : IAlertChannelSender
{
  private const int MaxBodyLength = 200;

  private readonly WebPushClient _webPushClient;
  private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
  private readonly IOptions<WebPushOptions> _options;
  private readonly IOptions<AlertOptions> _alertOptions;
  private readonly ILogger _logger;

  public NotificationChannel Channel => NotificationChannel.WebPush;

  public WebPushAlertChannelSender(
    WebPushClient webPushClient,
    IPushSubscriptionRepository pushSubscriptionRepository,
    IOptions<WebPushOptions> options,
    IOptions<AlertOptions> alertOptions,
    ILogger logger
  )
  {
    _webPushClient = webPushClient;
    _pushSubscriptionRepository = pushSubscriptionRepository;
    _options = options;
    _alertOptions = alertOptions;
    _logger = logger.ForContext<WebPushAlertChannelSender>();
  }

  public async Task<IReadOnlyList<DeliveryOutcome>> SendAsync(AlertBatch batch, CancellationToken cancellationToken)
  {
    if (batch.PushSubscriptionId is null)
    {
      _logger.Warning("WebPush batch for User {UserId} has no push subscription target, skipping", batch.UserId);
      return FailedAll(batch, DeliveryErrors.PushTargetMissing);
    }

    var pushSubscription = await _pushSubscriptionRepository.GetByIdAsync(batch.PushSubscriptionId.Value, batch.UserId, cancellationToken);

    if (pushSubscription is null || pushSubscription.Platform != PushPlatform.Web)
    {
      _logger.Warning("PushSubscription {PushSubscriptionId} not found or not a Web endpoint, skipping", batch.PushSubscriptionId);
      return FailedAll(batch, DeliveryErrors.PushSubscriptionGone);
    }

    var vapid = _options.Value;

    if (string.IsNullOrEmpty(vapid.VapidPrivateKey) || string.IsNullOrEmpty(vapid.VapidPublicKey) || string.IsNullOrEmpty(vapid.Subject))
    {
      _logger.Warning("Web Push VAPID is not configured, skipping {AlertCount} alerts", batch.Items.Count);
      return SkippedAll(batch, DeliveryErrors.PushNotConfigured);
    }

    var vapidDetails = new VapidDetails(vapid.Subject, vapid.VapidPublicKey, vapid.VapidPrivateKey);
    var target = new WebPush.PushSubscription(pushSubscription.Endpoint, pushSubscription.PublicKey, pushSubscription.AuthKey);

    var outcomes = new List<DeliveryOutcome>(batch.Items.Count);
    var remaining = batch.Items;

    for (var i = 0; i < remaining.Count; i++)
    {
      var item = remaining[i];

      try
      {
        await _webPushClient.SendNotificationAsync(target, BuildPayload(item), vapidDetails, cancellationToken);
        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent));
        _logger.Information("Web Push sent for User {UserId} ({Ticker} {Indicator})", batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
      }
      catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
      {
        await _pushSubscriptionRepository.DeleteAsync(pushSubscription.Id, batch.UserId, cancellationToken);
        _logger.Information("Push endpoint expired ({StatusCode}), removed PushSubscription {Id}", ex.StatusCode, pushSubscription.Id);
        outcomes.AddRange(FailRest(remaining, i, DeliveryErrors.PushEndpointExpired));
        return outcomes;
      }
      catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
      {
        _logger.Warning("Push service rate-limited (429), skipping remaining alerts for User {UserId}", batch.UserId);
        outcomes.AddRange(FailRest(remaining, i, DeliveryErrors.PushRateLimited));
        return outcomes;
      }
      catch (WebPushException ex)
      {
        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, $"Push service responded {(int)ex.StatusCode} {ex.StatusCode}"));
        _logger.Warning(ex, "Web Push failed for User {UserId} ({StatusCode})", batch.UserId, ex.StatusCode);
      }
    }

    return outcomes;
  }

  private string BuildPayload(RenderedAlertItem item)
  {
    var source = item.Source;

    var image = BuildImageUrl(source);

    var payload = new
    {
      title = $"{source.InstrumentTicker} - {source.IndicatorName}",
      body = Truncate(item.RenderedText, MaxBodyLength),
      tag = $"sub-{source.SubscriptionId}",
      icon = "/icons/icon-192.png",
      image,
      // Deep link into the dashboard subscription page
      data = new { path = $"/{AlertDeepLinks.SubscriptionPath(source.InstrumentTicker, source.SubscriptionId)}", ticker = source.InstrumentTicker }
    };

    return JsonSerializer.Serialize(payload);
  }

  private string? BuildImageUrl(AlertItem source)
  {
    var image = AlertDeepLinks.BuildChartImageUrl(_alertOptions.Value, source.TriggerId);

    if (image is null)
      _logger.Information("Alert image options are not configured (PublicBaseUrl/ImageSigningKey), sending text-only web push");

    return image;
  }

  private static IReadOnlyList<DeliveryOutcome> FailedAll(AlertBatch batch, string error) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  private static IReadOnlyList<DeliveryOutcome> SkippedAll(AlertBatch batch, string reason) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Skipped, reason))];

  private static IReadOnlyList<DeliveryOutcome> FailRest(IReadOnlyList<RenderedAlertItem> items, int fromIndex, string error) =>
    [.. items.Skip(fromIndex).Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..(maxLength - 1)]}…";
}
