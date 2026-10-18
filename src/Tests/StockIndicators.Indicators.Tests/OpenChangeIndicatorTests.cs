using LazyCache;
using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.OpenChange;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class OpenChangeIndicatorTests
{
  private const string Ticker = "AAPL";

  // One daily candle: open 100, close X - the handler compares them within the same candle.
  private static Candle[] Candles(decimal lastClose)
  {
    var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    var candles = Enumerable
      .Range(0, 5)
      .Select(i => new Candle(Ticker, start.AddDays(i), TimeFrame.Day, 100m, 102m, 98m, 100m, 1_000m, true, true, "NASDAQ"))
      .ToList();

    var last = candles[^1];
    candles[^1] = new Candle(Ticker, last.Timestamp, TimeFrame.Day, 100m, Math.Max(102m, lastClose), 98m, lastClose, 1_000m, true, true, "NASDAQ");

    return candles.ToArray();
  }

  private static (OpenChangeIndicatorCommandHandler Handler, Guid StateSlot) CreateHandler() =>
    (new OpenChangeIndicatorCommandHandler(new IndicatorInMemoryCache(new CachingService()), Logger.None), Guid.NewGuid());

  private static OpenChangeIndicatorTemplate Template(PriceChangeDirection direction = PriceChangeDirection.Both) =>
    new() { TimeFrame = TimeFrame.Day, Percent = 2, Direction = direction };

  [Fact]
  public async Task Growth_AboveOpen_TriggersOnce()
  {
    var (handler, stateSlot) = CreateHandler();

    var first = await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(), Candles(105m)), default);

    Assert.True(first.IsTriggered);
    Assert.Equal(5.0, first.Change, 3);

    // The move stays beyond the threshold: no repeated alert on the next evaluation
    var second = await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(), Candles(105m)), default);

    Assert.False(second.IsTriggered);
  }

  [Fact]
  public async Task BackBelowThreshold_ThenAgain_TriggerAgain()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template();

    Assert.True((await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(103m)), default)).IsTriggered);
    Assert.False((await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(100.5m)), default)).IsTriggered);
    Assert.True((await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, template, Candles(103m)), default)).IsTriggered);
  }

  [Fact]
  public async Task Drop_BelowOpen_TriggersForBoth()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(), Candles(95m)), default);

    Assert.True(result.IsTriggered);
    Assert.Equal(-5.0, result.Change, 3);
  }

  [Fact]
  public async Task ChangeWithinThreshold_DoesNotTrigger()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(), Candles(100.5m)), default);

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
      IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(direction), Candles(lastClose)), default);

    Assert.Equal(expected, result.IsTriggered);
  }

  [Fact]
  public async Task ZeroOpen_DoesNotTriggerOrThrow()
  {
    var (handler, stateSlot) = CreateHandler();

    var candles = Candles(105m);
    var last = candles[^1];
    candles[^1] = new Candle(Ticker, last.Timestamp, TimeFrame.Day, 0m, last.High, last.Low, last.Close, last.Volume, true, true, "NASDAQ");

    var result = await handler.Handle(IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(), candles), default);

    Assert.False(result.IsTriggered);
  }

  [Fact]
  public async Task EmptyCandles_DoesNotThrow()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(
      IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlot, Ticker, Template(), []), default);

    Assert.False(result.IsTriggered);
  }
}
