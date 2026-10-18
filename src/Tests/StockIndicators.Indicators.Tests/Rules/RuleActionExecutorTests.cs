using Moq;
using Newtonsoft.Json;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.EventBus;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Rules;
using StockIndicators.Contracts.Events;
using StockIndicators.Contracts.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

public class RuleActionExecutorTests
{
  private readonly Mock<IAlertDispatcher> _alertDispatcherMock;
  private readonly Mock<IEventBus> _eventBusMock;
  private readonly Mock<ITriggerHistoryRepository> _triggerHistoryRepositoryMock;
  private readonly Mock<ITriggerChartService> _triggerChartServiceMock;
  private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
  private readonly RuleActionExecutor _executor;

  private RuleTriggeredEvent? _publishedRuleEvent;
  private TriggerHistory? _storedHistory;

  private static readonly UserLocale EnLocale = new(UserLocale.English, TimeZoneInfo.Utc);

  // The real serializer: the fired condition the alert stores on its history row has to
  // survive the exact round trip the trigger chart will later read it through
  private static readonly ISerializer Serializer =
    new DefaultSerializer(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }, Logger.None);

  public RuleActionExecutorTests()
  {
    _alertDispatcherMock = new Mock<IAlertDispatcher>(MockBehavior.Strict);
    _eventBusMock = new Mock<IEventBus>(MockBehavior.Strict);
    _triggerHistoryRepositoryMock = new Mock<ITriggerHistoryRepository>(MockBehavior.Strict);
    _triggerChartServiceMock = new Mock<ITriggerChartService>(MockBehavior.Strict);
    _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>(MockBehavior.Strict);

    _alertDispatcherMock
      .Setup(dispatcher => dispatcher.AddAlert(
        It.IsAny<Subscription>(),
        It.IsAny<string>(),
        It.IsAny<string?>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<Guid>(),
        It.IsAny<byte[]?>(),
        It.IsAny<DateTime>()));
    _eventBusMock
      .Setup(bus => bus.PublishAsync<AlertEvent>(It.IsAny<AlertEvent>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    _eventBusMock
      .Setup(bus => bus.PublishAsync<RuleTriggeredEvent>(It.IsAny<RuleTriggeredEvent>(), It.IsAny<CancellationToken>()))
      .Callback<RuleTriggeredEvent, CancellationToken>((ruleEvent, _) => _publishedRuleEvent = ruleEvent)
      .Returns(Task.CompletedTask);
    _triggerHistoryRepositoryMock
      .Setup(repository => repository.AddAsync(It.IsAny<TriggerHistory>(), It.IsAny<CancellationToken>()))
      .Callback<TriggerHistory, CancellationToken>((history, _) => _storedHistory = history)
      .Returns(Task.CompletedTask);
    _triggerChartServiceMock
      .Setup(service => service.RenderPngAsync(It.IsAny<Subscription>(), It.IsAny<DateTime>(), It.IsAny<TriggerChartContext?>(), It.IsAny<UserLocale>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((byte[]?)null);

    _executor = new RuleActionExecutor(
      _alertDispatcherMock.Object,
      _eventBusMock.Object,
      _triggerHistoryRepositoryMock.Object,
      _triggerChartServiceMock.Object,
      _subscriptionRepositoryMock.Object,
      Serializer,
      Logger.None
    );
  }

  private static Subscription CreateSubscription() => new()
  {
    Id = Guid.NewGuid(),
    UserId = Guid.NewGuid(),
    Status = SubscriptionStatus.Active
  };

  private static Rule CreateRule(RuleAction[]? thenTrue, RuleAction[]? thenFalse, RuleGroup? when = null) => new()
  {
    Id = Guid.NewGuid(),
    Name = "Test rule",
    When = when ?? new RuleGroup { Id = Guid.NewGuid() },
    ThenTrue = thenTrue ?? [],
    ThenFalse = thenFalse ?? []
  };

  private static RuleCondition Condition(Indicator indicator, string ticker, string? exchange = null) =>
    new() { Id = Guid.NewGuid(), Indicator = indicator, Ticker = ticker, ExchangeName = exchange };

  // A WHEN tree is single-instrument only when every leaf watches the same ticker+exchange;
  // that decides whether alert lines carry their ticker prefix.
  private static RuleGroup Tree(params RuleCondition[] conditions) =>
    new() { Id = Guid.NewGuid(), Conditions = conditions.ToList() };

  private static RuleEvaluationResult Eval(bool met, params ConditionOutcome[] conditions) =>
    new(met, conditions);

  [Fact]
  public async Task Met_ThenTrueNotification_SendsAlert()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    // The alert identifies the instrument of the met condition, not of the subscription
    var condition = Condition(Indicator.SMA, "SBER");
    var executed = await _executor.ExecuteAsync(rule, Eval(true, new ConditionOutcome(condition, true, "100.0000")), subscription, EnLocale);

    Assert.True(executed);
    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "SBER", null, "Test rule", It.IsAny<string>(), It.IsAny<Guid>(), null, It.IsAny<DateTime>()),
      Times.Once);
    _triggerHistoryRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TriggerHistory>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Met_ThenTrueNotification_StoresItsOwnCopyOfTheFiredCondition()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    var condition = Condition(Indicator.SMA, "SBER");
    condition.TemplateData = """{"Periods":20,"TimeFrame":0}""";

    await _executor.ExecuteAsync(rule, Eval(true, new ConditionOutcome(condition, true, "100.0000")), subscription, EnLocale);

    Assert.NotNull(_storedHistory);
    Assert.Equal(condition.Id, _storedHistory!.TriggeredConditionId);

    // A later edit of the rule rebuilds the condition tree, so a bare id pointer is not enough:
    // the history row carries the condition itself, and it has to come back intact
    var snapshot = Serializer.DeserializeOrNull<RuleCondition>(_storedHistory.FiredConditionData!);

    Assert.NotNull(snapshot);
    Assert.Equal(condition.Id, snapshot!.Id);
    Assert.Equal(Indicator.SMA, snapshot.Indicator);
    Assert.Equal("SBER", snapshot.Ticker);
    Assert.Equal(condition.TemplateData, snapshot.TemplateData);
  }

  [Fact]
  public async Task NotMet_NoThenFalseActions_ReturnsFalseWithoutAlert()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    var executed = await _executor.ExecuteAsync(rule, Eval(false), subscription, EnLocale);

    Assert.False(executed);
    _alertDispatcherMock.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task NotMet_ThenFalseDisable_DisablesSubscription()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      null,
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.DisableSubscription }]
    );
    _subscriptionRepositoryMock
      .Setup(repository => repository.SetActiveAsync(subscription, false, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var executed = await _executor.ExecuteAsync(rule, Eval(false), subscription, EnLocale);

    Assert.True(executed);
    _subscriptionRepositoryMock.Verify(repository => repository.SetActiveAsync(subscription, false, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Met_ThenTrueDisable_PublishesRuleEvent()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.DisableSubscription }],
      null
    );
    _subscriptionRepositoryMock
      .Setup(repository => repository.SetActiveAsync(subscription, false, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var executed = await _executor.ExecuteAsync(rule, Eval(true), subscription, EnLocale);

    Assert.True(executed);
    Assert.NotNull(_publishedRuleEvent);
    Assert.True(_publishedRuleEvent!.ConditionMet);
    Assert.Equal(rule.Id, _publishedRuleEvent.RuleId);
    _eventBusMock.Verify(bus => bus.PublishAsync<RuleTriggeredEvent>(It.IsAny<RuleTriggeredEvent>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task FailedAction_DoesNotBlockOtherActions()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [
        new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification },
        new RuleAction { Id = Guid.NewGuid(), Type = ActionType.DisableSubscription }
      ],
      null
    );
    _triggerChartServiceMock
      .Setup(service => service.RenderPngAsync(subscription, It.IsAny<DateTime>(), It.IsAny<TriggerChartContext?>(), It.IsAny<UserLocale>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("render failed"));
    _subscriptionRepositoryMock
      .Setup(repository => repository.SetActiveAsync(subscription, false, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var condition = Condition(Indicator.SMA, "SBER");
    var executed = await _executor.ExecuteAsync(rule, Eval(true, new ConditionOutcome(condition, true, "100.0000")), subscription, EnLocale);

    Assert.True(executed);
    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "SBER", null, "Test rule", It.IsAny<string>(), It.IsAny<Guid>(), null, It.IsAny<DateTime>()),
      Times.Once);
  }

  [Fact]
  public async Task NoActions_ReturnsFalse()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(null, null);

    var executed = await _executor.ExecuteAsync(rule, Eval(true), subscription, EnLocale);

    Assert.False(executed);
    _alertDispatcherMock.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Met_NotificationListsFiredConditionsWithValues()
  {
    var subscription = CreateSubscription();

    // A single-instrument rule: the ticker would only repeat the channel line, so the
    // met lines stay bare indicator names
    var sma = Condition(Indicator.SMA, "SBER");
    var ema = Condition(Indicator.EMA, "SBER");
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null,
      Tree(sma, ema)
    );

    var evaluation = Eval(true,
      new ConditionOutcome(sma, true, "100.0000 (Upward)"),
      new ConditionOutcome(ema, true, "90.0000 (Upward)")
    );

    await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    const string expected =
      "triggered\n" +
      "• SMA: 100.0000 (Upward)\n" +
      "• EMA: 90.0000 (Upward)";

    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "SBER", null, "Test rule", expected, It.IsAny<Guid>(), null, It.IsAny<DateTime>()),
      Times.Once);
  }

  [Fact]
  public async Task Met_MultiInstrumentRule_PrefixesEveryMetLine()
  {
    var subscription = CreateSubscription();

    // The rule watches two instruments (SBER + AAPL), so every met line carries its ticker
    var sma = Condition(Indicator.SMA, "SBER");
    var gap = Condition(Indicator.Gap, "AAPL", "NASDAQ");
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null,
      Tree(sma, gap)
    );

    var evaluation = Eval(true,
      new ConditionOutcome(sma, true, "100.0000 (Upward)"),
      new ConditionOutcome(gap, true, "210.0000 (Upward)")
    );

    await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    const string expected =
      "triggered\n" +
      "• SBER SMA: 100.0000 (Upward)\n" +
      "• AAPL Gap: 210.0000 (Upward)";

    // The alert identity is the fired condition: the first met one, with its exchange
    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "SBER", null, "Test rule", expected, It.IsAny<Guid>(), null, It.IsAny<DateTime>()),
      Times.Once);
  }

  [Fact]
  public async Task Met_ForeignTickerConditionCarriesItsOwnIdentity()
  {
    var subscription = CreateSubscription();

    // Single-instrument-by-tree rules stay bare; when the tree itself spans two
    // instruments, even a lone met line is prefixed and the alert names that instrument
    var fired = Condition(Indicator.SMA, "AAPL");
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null,
      Tree(Condition(Indicator.EMA, "SBER"), fired)
    );

    var evaluation = Eval(true, new ConditionOutcome(fired, true, "210.0000 (Upward)"));

    await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    const string expected =
      "triggered\n" +
      "• AAPL SMA: 210.0000 (Upward)";

    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "AAPL", null, "Test rule", expected, It.IsAny<Guid>(), null, It.IsAny<DateTime>()),
      Times.Once);
  }

  [Fact]
  public async Task Met_HeaderIsLocalizedToUserLanguage()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    var sma = Condition(Indicator.SMA, "SBER");
    var ema = Condition(Indicator.EMA, "SBER");

    var evaluation = Eval(true,
      new ConditionOutcome(sma, true, "100.0000 (вверх)"),
      new ConditionOutcome(ema, true, null)
    );

    var ruLocale = new UserLocale(UserLocale.Russian, TimeZoneInfo.Utc);

    await _executor.ExecuteAsync(rule, evaluation, subscription, ruLocale);

    const string expected =
      "условие выполнено\n" +
      "• SMA: 100.0000 (вверх)\n" +
      "• EMA: выполнено";

    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "SBER", null, "Test rule", expected, It.IsAny<Guid>(), null, It.IsAny<DateTime>()),
      Times.Once);
  }

  [Fact]
  public async Task Met_Notification_StoresDetectionTimeAndTriggerCandle()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    // The candle cache can lag behind the wall clock (provider delay); the chart must render
    // around the candle the condition actually saw, while the history row and the alert carry
    // the detection moment so several triggers on one forming candle stay distinguishable.
    var candleDate = new DateTime(2026, 9, 16, 19, 30, 0, DateTimeKind.Utc);
    var beforeExecution = DateTime.UtcNow;

    var condition = Condition(Indicator.SMA, "SBER");
    var evaluation = Eval(true,
      new ConditionOutcome(condition, true, "100.0000 (Upward)", candleDate)
    );

    await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    _triggerHistoryRepositoryMock.Verify(
      repository => repository.AddAsync(
        It.Is<TriggerHistory>(history =>
          history.TriggeredCandleAt == candleDate &&
          history.TriggeredAt >= beforeExecution && history.TriggeredAt <= DateTime.UtcNow),
        It.IsAny<CancellationToken>()),
      Times.Once);
    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(subscription, "SBER", null, "Test rule", It.IsAny<string>(), It.IsAny<Guid>(), null,
        It.Is<DateTime>(triggeredAt => triggeredAt >= beforeExecution && triggeredAt <= DateTime.UtcNow)),
      Times.Once);
    _triggerChartServiceMock.Verify(
      service => service.RenderPngAsync(subscription, candleDate, It.IsAny<TriggerChartContext?>(), It.IsAny<UserLocale>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task Volume_Notification_SuppressedOnAlreadyNotifiedCandle()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    // Volume only accumulates within a candle: a re-fire on the candle that was already
    // notified is a pure duplicate. The previous row is legacy-shaped (the candle lives in
    // TriggeredAt), so the dedup must fall back to it across the schema change.
    var candleDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    var condition = Condition(Indicator.Volume, "BTCUSD", "BINANCE");
    var evaluation = Eval(true,
      new ConditionOutcome(condition, true, "2.4× avg", candleDate)
    );

    _triggerHistoryRepositoryMock
      .Setup(repository => repository.GetLatestByConditionAsync(subscription.Id, condition.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TriggerHistory { SubscriptionId = subscription.Id, TriggeredAt = candleDate, TriggeredConditionId = condition.Id });
    _subscriptionRepositoryMock
      .Setup(repository => repository.SetCooldownAsync(subscription, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var executed = await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    Assert.False(executed);
    _triggerHistoryRepositoryMock.Verify(
      repository => repository.AddAsync(It.IsAny<TriggerHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    _alertDispatcherMock.Verify(
      dispatcher => dispatcher.AddAlert(It.IsAny<Subscription>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
        It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<byte[]?>(), It.IsAny<DateTime>()), Times.Never);
    _subscriptionRepositoryMock.Verify(repository => repository.SetCooldownAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    _subscriptionRepositoryMock.Verify(repository => repository.SetTriggeredAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Volume_Notification_AlertsOnCandleNotNotifiedYet()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    var candleDate = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    var condition = Condition(Indicator.Volume, "BTCUSD", "BINANCE");
    var evaluation = Eval(true,
      new ConditionOutcome(condition, true, "2.1× avg", candleDate)
    );

    // The previous notification covers the day before — today's candle is new information
    _triggerHistoryRepositoryMock
      .Setup(repository => repository.GetLatestByConditionAsync(subscription.Id, condition.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TriggerHistory
      {
        SubscriptionId = subscription.Id,
        TriggeredAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
        TriggeredCandleAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
        TriggeredConditionId = condition.Id
      });

    var executed = await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    Assert.True(executed);
    _triggerHistoryRepositoryMock.Verify(
      repository => repository.AddAsync(It.Is<TriggerHistory>(history => history.TriggeredCandleAt == candleDate), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task FirstVolumeNotification_AlertsWithoutPreviousHistory()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    var condition = Condition(Indicator.Volume, "BTCUSD", "BINANCE");
    var evaluation = Eval(true,
      new ConditionOutcome(condition, true, "3.0× avg", new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc))
    );

    _triggerHistoryRepositoryMock
      .Setup(repository => repository.GetLatestByConditionAsync(subscription.Id, condition.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync((TriggerHistory?)null);

    var executed = await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    Assert.True(executed);
    _triggerHistoryRepositoryMock.Verify(
      repository => repository.AddAsync(It.IsAny<TriggerHistory>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task PriceCondition_RepeatsOnSameCandle_AreNotSuppressed()
  {
    var subscription = CreateSubscription();
    var rule = CreateRule(
      [new RuleAction { Id = Guid.NewGuid(), Type = ActionType.SendNotification }],
      null
    );

    // A price-driven condition oscillates within the forming candle — every re-entry is new
    // information, so the same-candle dedup must not even look at the history for it
    var candleDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    var condition = Condition(Indicator.SMA, "SBER");
    var evaluation = Eval(true,
      new ConditionOutcome(condition, true, "100.0000 (Upward)", candleDate)
    );

    var executed = await _executor.ExecuteAsync(rule, evaluation, subscription, EnLocale);

    Assert.True(executed);
    _triggerHistoryRepositoryMock.Verify(
      repository => repository.GetLatestByConditionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    _triggerHistoryRepositoryMock.Verify(
      repository => repository.AddAsync(It.IsAny<TriggerHistory>(), It.IsAny<CancellationToken>()), Times.Once);
  }
}
