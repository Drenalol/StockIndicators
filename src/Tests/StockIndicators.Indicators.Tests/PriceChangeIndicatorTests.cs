using LazyCache;
using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.PriceChange;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class PriceChangeIndicatorTests
{
  private const string Ticker = "AAPL";

  // Periods=10: the change is measured from the close 10 candles back, so the 11th
  // candle (index 10) is the first the handler can evaluate. The fixture ends on it.
  private const int CandleCount = 11;

  private static Candle[] Candles(decimal lastClose)
  {
    var start = new DateTime(2026, 1, 5, 15, 0, 0, DateTimeKind.Utc);

    var candles = Enumerable
      .Range(0, CandleCount)
      .Select(i => new Candle(Ticker, start.AddMinutes(i), TimeFrame.Minute, 100m, 100m, 100m, 100m, 1_000m, true, true, "NASDAQ"))
      .ToList();

    var last = candles[^1];
    candles[^1] = new Candle(Ticker, last.Timestamp, TimeFrame.Minute, 100m, lastClose, lastClose, lastClose, 1_000m, true, true, "NASDAQ");

    return candles.ToArray();
  }

  private static (PriceChangeIndicatorCommandHandler Handler, Guid StateSlot) CreateHandler() =>
    (new PriceChangeIndicatorCommandHandler(new IndicatorInMemoryCache(new CachingService()), Logger.None), Guid.NewGuid());

  private static PriceChangeIndicatorTemplate Template(PriceChangeDirection direction = PriceChangeDirection.Both) =>
    new() { TimeFrame = TimeFrame.Minute, Periods = 10, Percent = 1, Direction = direction };

  [Fact]
  public async Task Growth_AboveThreshold_TriggersOnce()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template();

    var first = await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(105m)), default);

    Assert.True(first.IsTriggered);
    Assert.Equal(5.0, first.Change, 3);

    // The move stays beyond the threshold: no repeated alert on the next evaluation
    var second = await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(105m)), default);

    Assert.False(second.IsTriggered);
  }

  [Fact]
  public async Task BackAboveThreshold_ThenAgain_TriggerAgain()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template();

    Assert.True((await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(102m)), default)).IsTriggered);
    Assert.False((await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(100.2m)), default)).IsTriggered);
    Assert.True((await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(103m)), default)).IsTriggered);
  }

  [Fact]
  public async Task Drop_AboveThreshold_TriggersForBoth()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, Template(), Candles(95m)), default);

    Assert.True(result.IsTriggered);
    Assert.Equal(-5.0, result.Change, 3);
  }

  [Fact]
  public async Task ChangeWithinThreshold_DoesNotTrigger()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, Template(), Candles(100.5m)), default);

    Assert.False(result.IsTriggered);
    Assert.Equal(0.5, result.Change, 3);
  }

  [Theory]
  [InlineData(PriceChangeDirection.Up, 105, true)]
  [InlineData(PriceChangeDirection.Up, 95, false)]
  [InlineData(PriceChangeDirection.Down, 95, true)]
  [InlineData(PriceChangeDirection.Down, 105, false)]
  [InlineData(PriceChangeDirection.Both, 105, true)]
  [InlineData(PriceChangeDirection.Both, 95, true)]
  public async Task Direction_DecidesWhichSideTriggers(PriceChangeDirection direction, decimal lastClose, bool expected)
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(
      IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, Template(direction), Candles(lastClose)), default);

    Assert.Equal(expected, result.IsTriggered);
  }

  [Fact]
  public async Task FewerCandlesThanPeriods_DoesNotTriggerOrThrow()
  {
    var (handler, stateSlot) = CreateHandler();
    var candles = Candles(105m)[..5];

    var result = await handler.Handle(
      IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlot, Ticker, Template(), candles), default);

    Assert.False(result.IsTriggered);
  }
}
