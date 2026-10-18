using System.Collections.Concurrent;
using System.Diagnostics;
using LazyCache;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Notifications.Rendering;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Notifications.Dispatcher;

/// <summary>
/// Collects alert items per scheduler run, resolves each subscription's enabled outputs and flushes
/// them per channel. Telegram is batched per destination chat, push channels are batched
/// per push subscription, URL/phone channels (Webhook/Discord/WhatsApp) per output target.
/// A failure in one channel batch never blocks the others. Every attempted delivery is
/// persisted as a <see cref="TriggerDelivery"/> row (date, status, error) so the trigger
/// history can show what actually happened per channel.
/// </summary>
public class AlertDispatcher : IAlertDispatcher
{
  private readonly IAlertTemplateRenderer _templateRenderer;
  private readonly INotificationTemplateRepository _templateRepository;
  private readonly IReadOnlyDictionary<NotificationChannel, IAlertChannelSender> _senders;
  private readonly IAppCache _appCache;
  private readonly IUserLocaleProvider _localeProvider;
  private readonly ITriggerDeliveryRepository _deliveryRepository;
  private readonly ILogger _logger;

  /// <summary>
  /// Keyed by UserId. Thread-safe for concurrent AddAlert calls during indicator evaluation.
  /// Values are defensively copied on AddOrUpdate to avoid List-of-AlertItem race conditions.
  /// </summary>
  private readonly ConcurrentDictionary<Guid, List<AlertItem>> _pendingAlerts = new();

  /// <summary>
  /// Keyed by SubscriptionId. Snapshot of enabled outputs taken at alert time,
  /// so a concurrent subscription patch cannot change an in-flight flush.
  /// </summary>
  private readonly ConcurrentDictionary<Guid, List<SubscriptionOutput>> _pendingOutputs = new();

  public AlertDispatcher(
    IAlertTemplateRenderer templateRenderer,
    INotificationTemplateRepository templateRepository,
    IEnumerable<IAlertChannelSender> senders,
    IAppCache appCache,
    IUserLocaleProvider localeProvider,
    ITriggerDeliveryRepository deliveryRepository,
    ILogger logger
  )
  {
    _templateRenderer = templateRenderer;
    _templateRepository = templateRepository;
    _senders = senders.ToDictionary(sender => sender.Channel);
    _appCache = appCache;
    _localeProvider = localeProvider;
    _deliveryRepository = deliveryRepository;
    _logger = logger.ForContext<AlertDispatcher>();
  }

  public void AddAlert(
    Subscription subscription,
    string instrumentTicker,
    string? exchangeName,
    string indicatorName,
    string alertText,
    Guid triggerId,
    byte[]? imagePng,
    DateTime triggeredAt
  )
  {
    var item = new AlertItem(subscription.UserId, subscription.Id, instrumentTicker, exchangeName, indicatorName, alertText, triggeredAt, triggerId, imagePng);

    _pendingAlerts.AddOrUpdate(
      subscription.UserId,
      _ => [item],
      (_, existing) =>
      {
        var copy = new List<AlertItem>(existing.Count + 1);
        copy.AddRange(existing);
        copy.Add(item);
        return copy;
      });

    _pendingOutputs.AddOrUpdate(subscription.Id, _ => OutputsSnapshot(subscription), (_, existing) => existing);

    BusinessMetrics.AlertEnqueued();
    BusinessMetrics.AlertQueueDepthSet(_pendingAlerts.Sum(group => group.Value.Count));

    List<SubscriptionOutput> OutputsSnapshot(Subscription s) => [.. s.Outputs.Where(o => o.IsEnabled)];
  }

  public async Task FlushAsync(CancellationToken cancellationToken = default)
  {
    var snapshots = _pendingAlerts.ToList();
    _pendingAlerts.Clear();
    var outputs = _pendingOutputs.ToList();
    _pendingOutputs.Clear();

    BusinessMetrics.AlertQueueDepthSet(0);

    if (snapshots.Count == 0)
      return;

    var totalAlerts = snapshots.Sum(s => s.Value.Count);
    BusinessMetrics.AlertFlushSizeRecord(totalAlerts);
    _logger.Information("Flushing {AlertGroups} alert groups ({TotalAlerts} alerts)", snapshots.Count, totalAlerts);

    CacheTriggerImages(snapshots);

    var templateCache = new Dictionary<Guid, NotificationTemplate?>();

    foreach (var (userId, alerts) in snapshots)
    {
      // One locale resolution per user per flush: alert text and {{time}} are rendered
      // in the user's language/timezone regardless of how many chats/push targets they have.
      var locale = await _localeProvider.GetAsync(userId, cancellationToken);

      foreach (var batch in await BuildBatchesAsync(userId, alerts, outputs, templateCache, locale, cancellationToken))
      {
        await SendAndRecordAsync(batch, cancellationToken);
      }
    }
  }

  /// <summary>
  /// Runs one channel batch and persists a delivery row per batch item. A missing sender or a
  /// throw is converted into explicit outcomes, so every trigger of the batch always gets its
  /// delivery record (with the date and the error the user can see in the trigger history).
  /// </summary>
  private async Task SendAndRecordAsync(AlertBatch batch, CancellationToken cancellationToken)
  {
    IReadOnlyList<DeliveryOutcome> outcomes;

    if (!_senders.TryGetValue(batch.Channel, out var sender))
    {
      _logger.Warning("No sender registered for channel {Channel}, skipping {AlertCount} alerts", batch.Channel, batch.Items.Count);
      outcomes = [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Skipped, DeliveryErrors.ChannelUnavailable))];
    }
    else
    {
      var sendStopwatch = Stopwatch.StartNew();

      try
      {
        outcomes = await sender.SendAsync(batch, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.Error(ex, "Failed to send {Channel} batch for User {UserId}", batch.Channel, batch.UserId);
        outcomes = [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, ex.Message))];
      }
      finally
      {
        BusinessMetrics.AlertSendDurationRecord(batch.Channel, sendStopwatch.Elapsed.TotalSeconds);
      }

      // Channels must report one outcome per item; anything missing is recorded explicitly
      var reported = outcomes.Select(o => o.TriggerId).ToHashSet();
      outcomes = [
        .. outcomes,
        .. batch.Items
          .Where(item => !reported.Contains(item.Source.TriggerId))
          .Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, DeliveryErrors.NoResult))
      ];
    }

    var sentAt = DateTime.UtcNow;

    foreach (var outcome in outcomes)
    {
      BusinessMetrics.AlertDelivery(batch.Channel, outcome.Status);

      await _deliveryRepository.AddAsync(
        new TriggerDelivery
        {
          Id = Guid.CreateVersion7(),
          TriggerHistoryId = outcome.TriggerId,
          Channel = batch.Channel,
          Status = outcome.Status,
          Error = outcome.Error,
          SentAt = sentAt
        },
        cancellationToken);
    }
  }

  /// <summary>
  /// Stores rendered PNGs so the public web-push image endpoint can serve them without re-rendering.
  /// </summary>
  private void CacheTriggerImages(List<KeyValuePair<Guid, List<AlertItem>>> snapshots)
  {
    foreach (var (_, alerts) in snapshots)
    {
      foreach (var alert in alerts)
      {
        if (alert.ImagePng is not { Length: > 0 } image)
          continue;

        _appCache.Add(
          TriggerChartImages.CacheKey(alert.TriggerId),
          image,
          DateTimeOffset.UtcNow.Add(TriggerChartImages.CacheTtl)
        );
      }
    }
  }

  private async Task<List<AlertBatch>> BuildBatchesAsync(
    Guid userId,
    List<AlertItem> alerts,
    List<KeyValuePair<Guid, List<SubscriptionOutput>>> outputs,
    Dictionary<Guid, NotificationTemplate?> templateCache,
    UserLocale locale,
    CancellationToken cancellationToken
  )
  {
    var batches = new List<AlertBatch>();

    // Telegram: one batch per destination chat. Outputs without a chat (pre-ChatId rows)
    // are sent to the user's default chat by the sender as a fallback.
    var telegramItems = alerts.Where(alert => EnabledOutput(outputs, alert.SubscriptionId, NotificationChannel.Telegram) is not null).ToList();

    foreach (var group in telegramItems.GroupBy(alert => EnabledOutput(outputs, alert.SubscriptionId, NotificationChannel.Telegram)!.ChatId))
    {
      var rendered = new List<RenderedAlertItem>(group.Count());

      foreach (var item in group)
      {
        var output = EnabledOutput(outputs, item.SubscriptionId, NotificationChannel.Telegram)!;
        var template = await GetTemplateAsync(output, templateCache, cancellationToken);
        rendered.Add(new RenderedAlertItem(item, _templateRenderer.RenderLine(NotificationChannel.Telegram, template, item, locale)));
      }

      batches.Add(new AlertBatch(NotificationChannel.Telegram, userId, null, group.Key, rendered, locale.Language));
    }

    // WebPush: one batch per push subscription target
    var webPushItems = alerts.Where(alert => EnabledOutput(outputs, alert.SubscriptionId, NotificationChannel.WebPush) is not null).ToList();

    foreach (var group in webPushItems.GroupBy(alert => EnabledOutput(outputs, alert.SubscriptionId, NotificationChannel.WebPush)!.PushSubscriptionId))
    {
      if (group.Key is null)
      {
        _logger.Warning("WebPush output without PushSubscriptionId on subscription, skipping {AlertCount} alerts", group.Count());
        continue;
      }

      var webPushRendered = new List<RenderedAlertItem>(group.Count());

      foreach (var item in group)
      {
        var output = EnabledOutput(outputs, item.SubscriptionId, NotificationChannel.WebPush)!;
        var template = await GetTemplateAsync(output, templateCache, cancellationToken);
        webPushRendered.Add(new RenderedAlertItem(item, _templateRenderer.RenderLine(NotificationChannel.WebPush, template, item, locale)));
      }

      batches.Add(new AlertBatch(NotificationChannel.WebPush, userId, group.Key, null, webPushRendered, locale.Language));
    }

    // Webhook/Discord/WhatsApp: one batch per output, the destination lives in output Target
    foreach (var channel in new[] { NotificationChannel.Webhook, NotificationChannel.Discord, NotificationChannel.WhatsApp })
    {
      var channelItems = alerts.Where(alert => EnabledOutput(outputs, alert.SubscriptionId, channel) is not null).ToList();

      if (channelItems.Count == 0)
        continue;

      // Validated one-output-per-channel makes the target identical across the user's
      // subscriptions only when they share it; grouping keeps per-subscription targets exact.
      foreach (var group in channelItems.GroupBy(alert => EnabledOutput(outputs, alert.SubscriptionId, channel)!.Id))
      {
        var renderedItems = new List<RenderedAlertItem>(group.Count());
        string? target = null;

        foreach (var item in group)
        {
          var output = EnabledOutput(outputs, item.SubscriptionId, channel)!;
          target = output.Target;

          var template = await GetTemplateAsync(output, templateCache, cancellationToken);
          renderedItems.Add(new RenderedAlertItem(item, _templateRenderer.RenderLine(channel, template, item, locale)));
        }

        batches.Add(new AlertBatch(channel, userId, null, null, renderedItems, locale.Language, target));
      }
    }

    return batches;
  }

  private async Task<NotificationTemplate?> GetTemplateAsync(
    SubscriptionOutput output,
    Dictionary<Guid, NotificationTemplate?> cache,
    CancellationToken cancellationToken
  )
  {
    if (output.NotificationTemplateId is null)
      return null;

    if (cache.TryGetValue(output.NotificationTemplateId.Value, out var template))
      return template;

    try
    {
      template = await _templateRepository.GetByIdAsync(output.NotificationTemplateId.Value, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.Warning(ex, "Failed to load notification template {TemplateId}, using default rendering", output.NotificationTemplateId);
      template = null;
    }

    cache[output.NotificationTemplateId.Value] = template;
    return template;
  }

  private static SubscriptionOutput? EnabledOutput(
    List<KeyValuePair<Guid, List<SubscriptionOutput>>> outputs,
    Guid subscriptionId,
    NotificationChannel channel
  )
  {
    foreach (var (key, list) in outputs)
    {
      if (key != subscriptionId)
        continue;

      var match = list.FirstOrDefault(o => o.Channel == channel);

      if (match != null)
        return match;
    }

    return null;
  }
}
