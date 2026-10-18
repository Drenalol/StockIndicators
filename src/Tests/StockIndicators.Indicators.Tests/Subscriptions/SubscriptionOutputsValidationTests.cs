using MediatR;
using Microsoft.AspNetCore.JsonPatch;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Subscriptions.CreateSubscription;
using StockIndicators.Application.Subscriptions.PatchSubscription;
using StockIndicators.Abstract;
using StockIndicators.Contracts.Candles;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Domain.Models.Telegram;
using Xunit;

namespace StockIndicators.Indicators.Tests.Subscriptions;

public class SubscriptionOutputsValidationTests
{
  private readonly Guid _userId = Guid.NewGuid();

  private CreateSubscriptionCommandHandler CreateCreateHandler(
    Mock<ISubscriptionRepository> subscriptions,
    List<TelegramChat> chats)
  {
    var telegramChats = new Mock<ITelegramChatRepository>();
    telegramChats
      .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(chats);

    return new CreateSubscriptionCommandHandler(
      subscriptions.Object,
      telegramChats.Object,
      new Mock<IPushSubscriptionRepository>().Object,
      new Mock<INotificationTemplateRepository>().Object,
      Enumerable.Empty<IInstrumentSource>(),
      new Mock<IMediator>().Object,
      Microsoft.Extensions.Options.Options.Create(new AlertOptions()));
  }

  // The rule the subscription container must carry: one EMA leaf on AAPL
  private static CreateRuleRequest RuleFixture() =>
    new()
    {
      Name = "rule",
      When = new RuleGroupDto(null, LogicalOperatorDto.And,
        [new RuleConditionDto(null, "EMA", null, ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value,
          null, null, null, 1m, null, "AAPL", null, TimeFrame.Day)],
        [])
    };

  private CreateSubscriptionCommand CreateCommand(IReadOnlyCollection<SubscriptionOutputRequest>? outputs) =>
    new(_userId, "AAPL", null, TimeSpan.FromMinutes(5), RuleFixture(), outputs);

  [Fact]
  public async Task Create_WithoutOutputsAndWithoutChats_Throws()
  {
    var subscriptions = new Mock<ISubscriptionRepository>();
    var handler = CreateCreateHandler(subscriptions, []);

    var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(CreateCommand(null), CancellationToken.None));

    Assert.Contains("at least one output channel", exception.Message);
    subscriptions.Verify(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Create_WithoutOutputsAndWithNotifyChat_DefaultsToThatChat()
  {
    var subscriptions = new Mock<ISubscriptionRepository>();
    Subscription? added = null;
    subscriptions
      .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
      .Callback((Subscription s, CancellationToken _) => added = s);

    var handler = CreateCreateHandler(subscriptions,
    [
      new TelegramChat { UserId = _userId, ChatId = 111 }
    ]);

    await handler.Handle(CreateCommand(null), CancellationToken.None);

    var output = Assert.Single(added!.Outputs);
    Assert.Equal(NotificationChannel.Telegram, output.Channel);
    Assert.Equal(111, output.ChatId);
  }

  [Fact]
  public async Task Patch_RemoveLastOutput_Throws()
  {
    var subscriptionId = Guid.NewGuid();
    var subscription = new Subscription
    {
      Id = subscriptionId,
      UserId = _userId,
      Cooldown = TimeSpan.FromMinutes(5),
    };

    var subscriptions = new Mock<ISubscriptionRepository>();
    subscriptions
      .Setup(r => r.GetByIdAsync(subscriptionId, _userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(subscription);
    subscriptions
      .Setup(r => r.GetOutputsAsync(subscriptionId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        new SubscriptionOutput
        {
          Id = Guid.NewGuid(),
          SubscriptionId = subscriptionId,
          Channel = NotificationChannel.Telegram,
          ChatId = 111,
        }
      ]);

    var telegramChats = new Mock<ITelegramChatRepository>();
    telegramChats
      .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);


    var handler = new PatchSubscriptionCommandHandler(
      subscriptions.Object,
      new Mock<ITriggerSnapshotRepository>().Object,
      telegramChats.Object,
      new Mock<IPushSubscriptionRepository>().Object,
      new Mock<INotificationTemplateRepository>().Object,
      Microsoft.Extensions.Options.Options.Create(new AlertOptions()),
      Serilog.Log.Logger);

    var patchDocument = JsonConvert.DeserializeObject<JsonPatchDocument>("[{\"op\":\"remove\",\"path\":\"/Outputs/0\"}]")!;

    var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
      new PatchSubscriptionCommand(subscriptionId, _userId, patchDocument), CancellationToken.None));

    Assert.Contains("at least one output channel", exception.Message);
    subscriptions.Verify(r => r.ReconcileOutputsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<SubscriptionOutput>>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  // A WebPush output whose PushSubscriptionId no longer resolves (disabled in another
  // tab/device or removed after the endpoint expired) must fail as 400 validation,
  // not as the Postgres foreign key violation (500) it used to abort with
  [Fact]
  public async Task Patch_WebPushWithStalePushSubscription_ThrowsValidation()
  {
    var (handler, subscriptions, pushSubscriptions) = CreatePatchHandler([], currentOutputs: []);

    var patchDocument = JsonConvert.DeserializeObject<JsonPatchDocument>(
      $$"""[{"op":"replace","path":"/Outputs","value":[{"id":"{{Guid.NewGuid()}}","channel":"WebPush","pushSubscriptionId":"{{Guid.NewGuid()}}","isEnabled":true}]}]""")!;

    var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
      new PatchSubscriptionCommand(Guid.NewGuid(), _userId, patchDocument), CancellationToken.None));

    Assert.Contains("no longer exists", exception.Message);
    pushSubscriptions.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), _userId, It.IsAny<CancellationToken>()), Times.Once);
    subscriptions.Verify(r => r.ReconcileOutputsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<SubscriptionOutput>>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Patch_WebPushWithoutPushSubscriptionId_ThrowsValidation()
  {
    var (handler, subscriptions, _) = CreatePatchHandler([], currentOutputs: []);

    var patchDocument = JsonConvert.DeserializeObject<JsonPatchDocument>(
      $$"""[{"op":"replace","path":"/Outputs","value":[{"id":"{{Guid.NewGuid()}}","channel":"WebPush","pushSubscriptionId":null,"isEnabled":true}]}]""")!;

    var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
      new PatchSubscriptionCommand(Guid.NewGuid(), _userId, patchDocument), CancellationToken.None));

    Assert.Contains("requires an enabled browser push subscription", exception.Message);
    subscriptions.Verify(r => r.ReconcileOutputsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<SubscriptionOutput>>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Patch_WebPushWithLivePushSubscription_Persists()
  {
    var pushSubscriptionId = Guid.NewGuid();
    var subscriptionId = Guid.NewGuid();

    var (handler, subscriptions, _) = CreatePatchHandler(
      [new PushSubscription { Id = pushSubscriptionId, UserId = _userId, Platform = PushPlatform.Web }],
      currentOutputs: [],
      subscriptionId: subscriptionId);

    var patchDocument = JsonConvert.DeserializeObject<JsonPatchDocument>(
      $$"""[{"op":"replace","path":"/Outputs","value":[{"id":"{{Guid.NewGuid()}}","channel":"WebPush","pushSubscriptionId":"{{pushSubscriptionId}}","isEnabled":true}]}]""")!;

    await handler.Handle(new PatchSubscriptionCommand(subscriptionId, _userId, patchDocument), CancellationToken.None);

    subscriptions.Verify(r => r.ReconcileOutputsAsync(
      subscriptionId,
      It.Is<IReadOnlyCollection<SubscriptionOutput>>(outputs =>
        outputs.Single().PushSubscriptionId == pushSubscriptionId),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  /// <summary>
  /// A patch handler over an empty subscription: <paramref name="livePushSubscriptions"/>
  /// are the only push subscriptions the user still has. The stored subscription gets
  /// <paramref name="subscriptionId"/> so the command id matches (the handler reconciles by it).
  /// </summary>
  private (PatchSubscriptionCommandHandler Handler, Mock<ISubscriptionRepository> Subscriptions, Mock<IPushSubscriptionRepository> PushSubscriptions) CreatePatchHandler(
    List<PushSubscription> livePushSubscriptions,
    List<SubscriptionOutput> currentOutputs,
    Guid? subscriptionId = null)
  {
    var subscription = new Subscription
    {
      Id = subscriptionId ?? Guid.NewGuid(),
      UserId = _userId,
      Cooldown = TimeSpan.FromMinutes(5),
    };

    var subscriptions = new Mock<ISubscriptionRepository>();
    subscriptions
      .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(subscription);
    subscriptions
      .Setup(r => r.GetOutputsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(currentOutputs);

    var telegramChats = new Mock<ITelegramChatRepository>();
    telegramChats
      .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var pushSubscriptions = new Mock<IPushSubscriptionRepository>();
    pushSubscriptions
      .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync((Guid id, Guid _, CancellationToken _) => livePushSubscriptions.FirstOrDefault(s => s.Id == id));

    var templates = new Mock<INotificationTemplateRepository>();
    templates
      .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Guid _, CancellationToken _) => null);


    var handler = new PatchSubscriptionCommandHandler(
      subscriptions.Object,
      new Mock<ITriggerSnapshotRepository>().Object,
      telegramChats.Object,
      pushSubscriptions.Object,
      templates.Object,
      Microsoft.Extensions.Options.Options.Create(new AlertOptions()),
      Serilog.Log.Logger);

    return (handler, subscriptions, pushSubscriptions);
  }
}
