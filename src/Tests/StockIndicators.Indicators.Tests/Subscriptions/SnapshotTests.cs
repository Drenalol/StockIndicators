using Moq;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;
using StockIndicators.Application.Subscriptions.RefreshSubscriptionSnapshot;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using Xunit;

namespace StockIndicators.Indicators.Tests.Subscriptions;

public class SnapshotTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly Guid _subscriptionId = Guid.NewGuid();
  private readonly Guid _ruleId = Guid.NewGuid();

  private readonly Subscription _subscription;
  private readonly Rule _rule;

  private readonly Mock<ISubscriptionRepository> _subscriptionRepository = new();
  private readonly Mock<ITriggerSnapshotRepository> _snapshotRepository = new();
  private readonly Mock<IRuleRepository> _ruleRepository = new();
  private readonly Mock<ISubscriptionSnapshotRenderer> _renderer = new();
  private readonly Mock<ISubscriptionCache> _subscriptionCache = new();

  public SnapshotTests()
  {
    _subscription = new()
    {
      Id = _subscriptionId,
      UserId = _userId,
      Cooldown = TimeSpan.FromMinutes(30),
      Status = SubscriptionStatus.Active,
      RuleGroupId = _ruleId
    };

    _rule = new()
    {
      Id = _ruleId,
      UserId = _userId,
      Name = "rule",
      When = new RuleGroup
      {
        Id = _ruleId,
        LogicalOperator = LogicalOperator.And,
        Conditions =
        [
          new RuleCondition
          {
            Indicator = Indicator.EMA,
            TemplateData = null,
            Ticker = "AAPL",
            ExchangeName = null,
            TimeFrame = TimeFrame.Day
          }
        ]
      }
    };

    // The repository impl throws for a missing/foreign subscription - the mock mirrors it
    _subscriptionRepository
      .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
      .Returns((Guid id, Guid? userId, CancellationToken ct) =>
        id == _subscriptionId ? Task.FromResult(_subscription) : Task.FromException<Subscription>(new SubscriptionNotFoundException()));

    _ruleRepository
      .Setup(r => r.GetByIdAsync(_ruleId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(_rule);

    _subscriptionCache
      .Setup(c => c.IsTriggeredAsync(_subscription, It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    _renderer
      .Setup(r => r.RenderAsync(_subscription, _rule, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[] { RenderedChart(_subscriptionId) });
  }

  private GetSubscriptionSnapshotQueryHandler CreateGetHandler() =>
    new(
      _subscriptionRepository.Object,
      _snapshotRepository.Object,
      _ruleRepository.Object,
      _renderer.Object,
      _subscriptionCache.Object);

  private RefreshSubscriptionSnapshotCommandHandler CreateRefreshHandler() =>
    new(
      _subscriptionRepository.Object,
      _snapshotRepository.Object,
      _ruleRepository.Object,
      _renderer.Object,
      _subscriptionCache.Object);

  private static TriggerSnapshot RenderedChart(Guid subscriptionId) => new()
  {
    Id = Guid.NewGuid(),
    SubscriptionId = subscriptionId,
    Ticker = "AAPL",
    ExchangeName = string.Empty,
    TimeFrame = TimeFrame.Day,
    IndicatorNames = "EMA 21",
    LastCandleAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
    RenderedAt = DateTime.UtcNow
  };

  [Fact]
  public async Task Get_CacheMiss_RendersOnce_SecondGetUsesTheStore()
  {
    var stored = new List<TriggerSnapshot>();
    _snapshotRepository
      .Setup(r => r.GetBySubscriptionIdAsync(_subscriptionId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(() => stored.ToArray());
    _snapshotRepository
      .Setup(r => r.ReplaceAsync(_subscriptionId, It.IsAny<IReadOnlyList<TriggerSnapshot>>(), It.IsAny<CancellationToken>()))
      .Callback<Guid, IReadOnlyList<TriggerSnapshot>, CancellationToken>((id, snapshots, _) => stored.AddRange(snapshots))
      .Returns(Task.CompletedTask);
    _renderer
      .Setup(r => r.RenderAsync(_subscription, _rule, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[] { RenderedChart(_subscriptionId) });

    var handler = CreateGetHandler();

    var first = await handler.Handle(new GetSubscriptionSnapshotQuery(_subscriptionId, _userId), CancellationToken.None);

    Assert.Single(first.Charts);
    Assert.Equal("AAPL", first.Charts[0].Ticker);
    Assert.Null(first.Charts[0].ExchangeName);
    Assert.Equal("Day", first.Charts[0].TimeFrame);
    Assert.Equal(["EMA 21"], first.Charts[0].Indicators);
    Assert.NotNull(first.RenderedAt);
    _renderer.Verify(
      r => r.RenderAsync(It.IsAny<Subscription>(), It.IsAny<Rule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Once);

    var second = await handler.Handle(new GetSubscriptionSnapshotQuery(_subscriptionId, _userId), CancellationToken.None);

    Assert.Single(second.Charts);
    _renderer.Verify(
      r => r.RenderAsync(It.IsAny<Subscription>(), It.IsAny<Rule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Once); // the second visit reads the stored charts
  }

  [Fact]
  public async Task Get_ForeignSubscription_ThrowsNotFound()
  {
    var handler = CreateGetHandler();

    await Assert.ThrowsAsync<SubscriptionNotFoundException>(
      () => handler.Handle(new GetSubscriptionSnapshotQuery(Guid.NewGuid(), _userId), CancellationToken.None));

    _renderer.Verify(
      r => r.RenderAsync(It.IsAny<Subscription>(), It.IsAny<Rule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task Get_RendererYieldsNothing_ThrowsSnapshotNotFound()
  {
    _snapshotRepository
      .Setup(r => r.GetBySubscriptionIdAsync(_subscriptionId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<TriggerSnapshot>());
    _renderer
      .Setup(r => r.RenderAsync(_subscription, _rule, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((TriggerSnapshot[]?)null);

    var handler = CreateGetHandler();

    await Assert.ThrowsAsync<SnapshotNotFoundException>(
      () => handler.Handle(new GetSubscriptionSnapshotQuery(_subscriptionId, _userId), CancellationToken.None));

    _snapshotRepository.Verify(
      r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<TriggerSnapshot>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task Refresh_Success_Renders_AndReplacesTheStoredRows()
  {
    var handler = CreateRefreshHandler();

    var dto = await handler.Handle(new RefreshSubscriptionSnapshotCommand(_subscriptionId, _userId), CancellationToken.None);

    Assert.Single(dto.Charts);
    Assert.NotNull(dto.RenderedAt);
    _snapshotRepository.Verify(
      r => r.ReplaceAsync(_subscriptionId, It.IsAny<IReadOnlyList<TriggerSnapshot>>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task Refresh_RenderYieldsNothing_ThrowsSnapshotNotFound_AndReplacesNothing()
  {
    _renderer
      .Setup(r => r.RenderAsync(_subscription, _rule, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((TriggerSnapshot[]?)null);

    var handler = CreateRefreshHandler();

    await Assert.ThrowsAsync<SnapshotNotFoundException>(
      () => handler.Handle(new RefreshSubscriptionSnapshotCommand(_subscriptionId, _userId), CancellationToken.None));

    _snapshotRepository.Verify(
      r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<TriggerSnapshot>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task Refresh_RenderThrows_Propagates_AndReplacesNothing()
  {
    _renderer
      .Setup(r => r.RenderAsync(_subscription, _rule, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("render exploded"));

    var handler = CreateRefreshHandler();

    await Assert.ThrowsAsync<InvalidOperationException>(
      () => handler.Handle(new RefreshSubscriptionSnapshotCommand(_subscriptionId, _userId), CancellationToken.None));

    _snapshotRepository.Verify(
      r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<TriggerSnapshot>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task SnapshotChart_OwnershipGate_ForeignSubscriptionThrows()
  {
    // The chart data route is reachable only through the subscription: the ownership check
    // fails before any candle/indicator work happens
    var handler = new StockIndicators.Application.Subscriptions.GetSnapshotChartData.GetSnapshotChartDataQueryHandler(
      _subscriptionRepository.Object,
      _snapshotRepository.Object,
      new Mock<IRuleRepository>().Object,
      new Mock<ISubscriptionCache>().Object,
      new Mock<StockIndicators.Application.Charts.ITriggerChartService>().Object,
      new Mock<IUserLocaleProvider>().Object);

    await Assert.ThrowsAsync<SubscriptionNotFoundException>(
      () => handler.Handle(
        new StockIndicators.Application.Subscriptions.GetSnapshotChartData.GetSnapshotChartDataQuery(Guid.NewGuid(), Guid.NewGuid(), _userId),
        CancellationToken.None));
  }
}
