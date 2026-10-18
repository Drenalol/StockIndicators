using LazyCache;
using Moq;
using Serilog.Core;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Notifications.Rendering;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.Subscriptions;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class AlertDispatcherTests
{
  private static readonly UserLocale TestLocale = new(UserLocale.English, TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

  private readonly Guid _userId = Guid.NewGuid();
  private readonly Mock<IAlertTemplateRenderer> _renderer = new();
  private readonly Mock<INotificationTemplateRepository> _templates = new();
  private readonly Mock<IAlertChannelSender> _telegramSender = new();
  private readonly Mock<IUserLocaleProvider> _localeProvider = new();
  private readonly Mock<ITriggerDeliveryRepository> _deliveries = new();
  private readonly List<AlertBatch> _telegramBatches = new();
  private readonly List<TriggerDelivery> _recordedDeliveries = new();

  public AlertDispatcherTests()
  {
    _renderer
      .Setup(r => r.RenderLine(It.IsAny<NotificationChannel>(), It.IsAny<NotificationTemplate?>(), It.IsAny<AlertItem>(), It.IsAny<UserLocale>()))
      .Returns((NotificationChannel channel, NotificationTemplate? template, AlertItem item, UserLocale locale) => item.AlertText);

    _localeProvider
      .Setup(p => p.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(TestLocale);

    _templates
      .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((NotificationTemplate?)null);

    _telegramSender
      .SetupGet(s => s.Channel)
      .Returns(NotificationChannel.Telegram);

    _telegramSender
      .Setup(s => s.SendAsync(It.IsAny<AlertBatch>(), It.IsAny<CancellationToken>()))
      .Callback<AlertBatch, CancellationToken>((batch, _) => _telegramBatches.Add(batch))
      .ReturnsAsync((AlertBatch batch, CancellationToken _) =>
        (IReadOnlyList<DeliveryOutcome>)[.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent))]);

    _deliveries
      .Setup(r => r.AddAsync(It.IsAny<TriggerDelivery>(), It.IsAny<CancellationToken>()))
      .Callback<TriggerDelivery, CancellationToken>((delivery, _) => _recordedDeliveries.Add(delivery))
      .Returns(Task.CompletedTask);
  }

  private AlertDispatcher CreateDispatcher() =>
    new(
      _renderer.Object,
      _templates.Object,
      [_telegramSender.Object],
      new Mock<LazyCache.IAppCache>().Object,
      _localeProvider.Object,
      _deliveries.Object,
      Logger.None
    );

  private static Subscription SubscriptionWithOutputs(Guid userId, params (NotificationChannel Channel, long? ChatId)[] outputs)
  {
    var subscription = new Subscription { Id = Guid.NewGuid(), UserId = userId, Status = SubscriptionStatus.Active };

    subscription.Outputs =
    [
      .. outputs.Select(
        output =>
          new SubscriptionOutput
          {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            Channel = output.Channel,
            ChatId = output.ChatId,
            IsEnabled = true
          })
    ];

    return subscription;
  }

  [Fact]
  public async Task Flush_GroupsTelegramAlertsPerChat()
  {
    var dispatcher = CreateDispatcher();

    var subscriptionA = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));
    var subscriptionB = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 222));

    dispatcher.AddAlert(subscriptionA, "AAPL", null, "SMA", "a", Guid.NewGuid(), null, DateTime.UtcNow);
    dispatcher.AddAlert(subscriptionB, "MSFT", null, "EMA", "b", Guid.NewGuid(), null, DateTime.UtcNow);
    dispatcher.AddAlert(subscriptionA, "AAPL", null, "SMA", "c", Guid.NewGuid(), null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    // One batch per destination chat; both alerts of subscriptionA go to the same chat
    Assert.Equal(2, _telegramBatches.Count);

    var batchA = _telegramBatches.Single(batch => batch.ChatId == 111);
    var batchB = _telegramBatches.Single(batch => batch.ChatId == 222);

    Assert.Equal(2, batchA.Items.Count);
    Assert.Single(batchB.Items);
  }

  [Fact]
  public async Task Flush_MergesAlertsOfTheSameChatAcrossSubscriptions()
  {
    var dispatcher = CreateDispatcher();

    var subscriptionA = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));
    var subscriptionB = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));

    dispatcher.AddAlert(subscriptionA, "AAPL", null, "SMA", "a", Guid.NewGuid(), null, DateTime.UtcNow);
    dispatcher.AddAlert(subscriptionB, "MSFT", null, "EMA", "b", Guid.NewGuid(), null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    var batch = Assert.Single(_telegramBatches);
    Assert.Equal(111, batch.ChatId);
    Assert.Equal(2, batch.Items.Count);
  }

  [Fact]
  public async Task Flush_KeepsLegacyBatchesWithoutChat()
  {
    var dispatcher = CreateDispatcher();

    var subscription = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, null));

    dispatcher.AddAlert(subscription, "AAPL", null, "SMA", "a", Guid.NewGuid(), null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    var batch = Assert.Single(_telegramBatches);
    Assert.Null(batch.ChatId);
  }

  [Fact]
  public async Task Flush_CarriesUserLanguageIntoBatchesAndRenderer()
  {
    var dispatcher = CreateDispatcher();

    var subscription = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));

    dispatcher.AddAlert(subscription, "AAPL", null, "SMA", "a", Guid.NewGuid(), null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    _localeProvider.Verify(p => p.GetAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    _renderer.Verify(
      r => r.RenderLine(It.IsAny<NotificationChannel>(), It.IsAny<NotificationTemplate?>(), It.IsAny<AlertItem>(), It.Is<UserLocale>(locale => locale == TestLocale)),
      Times.Once);

    var batch = Assert.Single(_telegramBatches);
    Assert.Equal(UserLocale.English, batch.Language);
  }

  [Fact]
  public async Task Flush_RecordsOneDeliveryRowPerAlertAndChannel()
  {
    var dispatcher = CreateDispatcher();

    var subscription = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));
    var triggerId = Guid.NewGuid();

    dispatcher.AddAlert(subscription, "AAPL", null, "SMA", "a", triggerId, null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    var delivery = Assert.Single(_recordedDeliveries);
    Assert.Equal(triggerId, delivery.TriggerHistoryId);
    Assert.Equal(NotificationChannel.Telegram, delivery.Channel);
    Assert.Equal(DeliveryStatus.Sent, delivery.Status);
    Assert.Null(delivery.Error);
    Assert.True(delivery.SentAt > DateTime.UtcNow.AddMinutes(-1));
  }

  [Fact]
  public async Task Flush_RecordsFailedDeliveryWithErrorWhenSenderThrows()
  {
    _telegramSender
      .Setup(s => s.SendAsync(It.IsAny<AlertBatch>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new HttpRequestException("telegram is down"));

    var dispatcher = CreateDispatcher();

    var subscription = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));
    var triggerId = Guid.NewGuid();

    dispatcher.AddAlert(subscription, "AAPL", null, "SMA", "a", triggerId, null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    var delivery = Assert.Single(_recordedDeliveries);
    Assert.Equal(DeliveryStatus.Failed, delivery.Status);
    Assert.Equal("telegram is down", delivery.Error);
  }

  [Fact]
  public async Task Flush_RecordsFailedDeliveryWhenSenderReportsNoOutcome()
  {
    _telegramSender
      .Setup(s => s.SendAsync(It.IsAny<AlertBatch>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var dispatcher = CreateDispatcher();

    var subscription = SubscriptionWithOutputs(_userId, (NotificationChannel.Telegram, 111));
    var triggerId = Guid.NewGuid();

    dispatcher.AddAlert(subscription, "AAPL", null, "SMA", "a", triggerId, null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    var delivery = Assert.Single(_recordedDeliveries);
    Assert.Equal(DeliveryStatus.Failed, delivery.Status);
    Assert.Equal(DeliveryErrors.NoResult, delivery.Error);
  }

  [Fact]
  public async Task Flush_RecordsSkippedDeliveryForChannelWithoutSender()
  {
    var dispatcher = CreateDispatcher();

    var subscription = SubscriptionWithOutputs(_userId, (NotificationChannel.Discord, null));
    var triggerId = Guid.NewGuid();

    dispatcher.AddAlert(subscription, "AAPL", null, "SMA", "a", triggerId, null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    var delivery = Assert.Single(_recordedDeliveries);
    Assert.Equal(NotificationChannel.Discord, delivery.Channel);
    Assert.Equal(DeliveryStatus.Skipped, delivery.Status);
    Assert.Equal(DeliveryErrors.ChannelUnavailable, delivery.Error);
  }

  [Fact]
  public async Task Flush_BatchesUrlChannelsPerOutputTarget()
  {
    var webhookSender = new Mock<IAlertChannelSender>();
    webhookSender.SetupGet(s => s.Channel).Returns(NotificationChannel.Webhook);
    webhookSender
      .Setup(s => s.SendAsync(It.IsAny<AlertBatch>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((AlertBatch batch, CancellationToken _) =>
        (IReadOnlyList<DeliveryOutcome>)[.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent))]);

    var dispatcher = new AlertDispatcher(
      _renderer.Object,
      _templates.Object,
      [webhookSender.Object],
      new Mock<LazyCache.IAppCache>().Object,
      _localeProvider.Object,
      _deliveries.Object,
      Logger.None);

    var subscriptionA = SubscriptionWithOutputs(_userId, (NotificationChannel.Webhook, null));
    var subscriptionB = SubscriptionWithOutputs(_userId, (NotificationChannel.Webhook, null));

    dispatcher.AddAlert(subscriptionA, "AAPL", null, "SMA", "a", Guid.NewGuid(), null, DateTime.UtcNow);
    dispatcher.AddAlert(subscriptionB, "MSFT", null, "EMA", "b", Guid.NewGuid(), null, DateTime.UtcNow);

    await dispatcher.FlushAsync();

    // Each subscription owns its Webhook destination, so batches never merge across outputs
    webhookSender.Verify(
      s => s.SendAsync(It.Is<AlertBatch>(batch => batch.Channel == NotificationChannel.Webhook), It.IsAny<CancellationToken>()),
      Times.Exactly(2));
  }
}
