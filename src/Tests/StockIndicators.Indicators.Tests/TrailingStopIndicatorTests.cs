using LazyCache;
using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.TrailingStop;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class TrailingStopIndicatorTests
{
  private const string Ticker = "TSLA";
  private static readonly DateTime Start = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

  /// <summary>
  /// A fresh candle series whose last candle carries the tested high/low/close; the earlier
  /// candles are a flat 100 so the anchor arithmetic is easy to read.
  /// </summary>
  private static Candle[] Series(decimal lastHigh, decimal lastLow, decimal lastClose, int count = 20)
  {
    var candles = Enumerable
      .Range(0, count)
      .Select(i => new Candle(Ticker, Start.AddMinutes(i), TimeFrame.Minute, 100m, 100m, 100m, 100m, 1_000m, true, true, "NASDAQ"))
      .ToList();

    candles[^1] = new Candle(Ticker, Start.AddMinutes(count - 1), TimeFrame.Minute, 100m, lastHigh, lastLow, lastClose, 1_000m, true, true, "NASDAQ");

    return candles.ToArray();
  }

  private sealed class FakeAnchorStore : ITrailingStopAnchorStore
  {
    public Dictionary<Guid, double> Anchors { get; } = [];

    public Task<double?> GetAnchorAsync(Guid conditionId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Anchors.TryGetValue(conditionId, out var anchor) ? (double?)anchor : null);

    public Task SetAnchorAsync(Guid conditionId, double anchor, CancellationToken cancellationToken = default)
    {
      Anchors[conditionId] = anchor;
      return Task.CompletedTask;
    }

    public Task RemoveAnchorsAsync(IReadOnlyCollection<Guid> conditionIds, CancellationToken cancellationToken = default)
    {
      foreach (var id in conditionIds)
        Anchors.Remove(id);

      return Task.CompletedTask;
    }
  }

  private static (TrailingStopIndicatorCommandHandler Handler, Guid StateSlot) CreateHandler(FakeAnchorStore? store = null) =>
    (new TrailingStopIndicatorCommandHandler(new IndicatorInMemoryCache(new CachingService()), Logger.None, store), Guid.NewGuid());

  private static TrailingStopIndicatorTemplate Template(
    ChandelierDirection type = ChandelierDirection.Long,
    TrailingThresholdType thresholdType = TrailingThresholdType.Percent,
    double value = 5) =>
    new() { TimeFrame = TimeFrame.Minute, Type = type, ThresholdType = thresholdType, Value = value };

  [Fact]
  public async Task Long_DropThroughStopLine_TriggersOnceAndRearms()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template();

    // Arming: the anchor starts at the current high; the close stays above the line
    var armed = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 100m, 100m)), default);

    Assert.False(armed.IsTriggered);
    Assert.Equal(100, armed.Anchor!.Value, 3);
    Assert.Equal(95, armed.Line, 3);

    // Stop hit: the close drops 6% under the anchor - past the 5% line
    var hit = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 93m, 94m)), default);

    Assert.True(hit.IsTriggered);
    Assert.Equal(-6.0, hit.Change, 3);
    Assert.Equal(TrailingSide.Below, hit.Side);

    // The close stays below the line: no repeated alert
    Assert.False((await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 93m, 94.5m)), default)).IsTriggered);

    // Crossing back re-arms, and the next drop-through fires again
    Assert.False((await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 96m, 96m)), default)).IsTriggered);

    Assert.True((await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 93m, 94m)), default)).IsTriggered);
  }

  [Fact]
  public async Task Long_StopFollowsNewHighs()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template();

    // The rally ratchets the anchor and the line up to 105
    await handler.Handle(IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 100m, 100m)), default);
    var ratcheted = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(110m, 100m, 110m)), default);

    Assert.Equal(110, ratcheted.Anchor!.Value, 3);
    Assert.Equal(104.5, ratcheted.Line, 3);

    // 105 is above the new line (survives), 104 crosses it (alert)
    Assert.False((await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(110m, 104m, 105m)), default)).IsTriggered);
    Assert.True((await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(110m, 103m, 104m)), default)).IsTriggered);
  }

  [Fact]
  public async Task Long_AbsoluteThreshold_DropsTheLineByAFixedAmount()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template(thresholdType: TrailingThresholdType.Absolute, value: 3);

    await handler.Handle(IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 100m, 100m)), default);

    var hit = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 96m, 96.5m)), default);

    Assert.Equal(97, hit.Line, 3);
    Assert.True(hit.IsTriggered);
  }

  [Fact]
  public async Task Short_RiseThroughStopLine_TriggersAndPointsUp()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template(type: ChandelierDirection.Short);

    await handler.Handle(IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 100m, 100m)), default);

    // The short anchors on the lowest low; a close 6% above it breaks the 5% stop above
    var hit = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(107m, 100m, 106m)), default);

    Assert.True(hit.IsTriggered);
    Assert.Equal(100, hit.Anchor!.Value, 3);
    Assert.Equal(105, hit.Line, 3);
    Assert.Equal(6.0, hit.Change, 3);
    Assert.Equal(TrailingSide.Above, hit.Side);
  }

  [Fact]
  public async Task Anchor_PersistsOnArmingAndRatchets()
  {
    var store = new FakeAnchorStore();
    var (handler, stateSlot) = CreateHandler(store);
    var template = Template();

    await handler.Handle(IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(100m, 100m, 100m)), default);

    Assert.Equal(100, store.Anchors[stateSlot], 3);

    await handler.Handle(IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, template, Series(110m, 100m, 110m)), default);

    Assert.Equal(110, store.Anchors[stateSlot], 3);
  }

  [Fact]
  public async Task Restart_HydratesAnchorFromTheStore()
  {
    var store = new FakeAnchorStore();
    var template = Template();
    var conditionId = Guid.NewGuid();
    store.Anchors[conditionId] = 200;

    // A cold in-memory state (fresh cache) with a persisted anchor of 200: the anchor must
    // continue from 200 (ratcheting with the new high), not re-anchor at the current price
    IIndicatorCache coldCache = new IndicatorInMemoryCache(new CachingService());
    var handler = new TrailingStopIndicatorCommandHandler(coldCache, Logger.None, store);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(conditionId, Ticker, template, Series(210m, 200m, 205m)), default);

    Assert.Equal(210, result.Anchor!.Value, 3);
    Assert.Equal(199.5, result.Line, 3);
    Assert.False(result.IsTriggered);
    Assert.Equal(210, store.Anchors[conditionId], 3);
  }

  [Fact]
  public async Task StoreFailures_NeverBreakEvaluation()
  {
    var template = Template();
    var handler = new TrailingStopIndicatorCommandHandler(
      new IndicatorInMemoryCache(new CachingService()), Logger.None, new BrokenAnchorStore());

    var result = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(Guid.NewGuid(), Ticker, template, Series(100m, 100m, 94m)), default);

    // Arming (anchor = current high) survives the broken store; the first check cannot fire anyway
    Assert.False(result.IsTriggered);
    Assert.Equal(100, result.Anchor!.Value, 3);
  }

  private sealed class BrokenAnchorStore : ITrailingStopAnchorStore
  {
    public Task<double?> GetAnchorAsync(Guid conditionId, CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("db is down");

    public Task SetAnchorAsync(Guid conditionId, double anchor, CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("db is down");

    public Task RemoveAnchorsAsync(IReadOnlyCollection<Guid> conditionIds, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  [Fact]
  public async Task EmptyCandles_DoesNotThrow()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(
      IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlot, Ticker, Template(), []), default);

    Assert.False(result.IsTriggered);
  }
}
