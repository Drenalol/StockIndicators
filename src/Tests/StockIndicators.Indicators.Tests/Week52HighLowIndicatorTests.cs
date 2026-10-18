using LazyCache;
using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Week52HighLow;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class Week52HighLowIndicatorTests
{
  private const string Ticker = "AAPL";
  private static readonly DateTime Start = new(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);

  // One year plus a week of synthetic daily candles with a flat 100/105/95 profile.
  private static Candle Flat(int day, decimal close) =>
    new(Ticker, Start.AddDays(day), TimeFrame.Day, 100m, 105m, 95m, close, 1_000m, true, true, "NASDAQ");

  /// <summary>
  /// Consecutive daily candles ending <paramref name="days"/> after the start; the last one
  /// carries the tested close (its high stays at 105 - extremes are measured over the window
  /// excluding the last candle).
  /// </summary>
  private static Candle[] Series(int days, decimal lastClose, decimal lastHigh = 105m)
  {
    var candles = Enumerable.Range(0, days + 1).Select(day => Flat(day, 100m)).ToList();

    var last = candles[^1];
    candles[^1] = new Candle(Ticker, last.Timestamp, TimeFrame.Day, 100m, lastHigh, 95m, lastClose, 1_000m, true, true, "NASDAQ");

    return candles.ToArray();
  }

  private static (Week52HighLowIndicatorCommandHandler Handler, Guid StateSlot) CreateHandler() =>
    (new Week52HighLowIndicatorCommandHandler(new IndicatorInMemoryCache(new CachingService()), Logger.None), Guid.NewGuid());

  private static Week52HighLowIndicatorTemplate Template(
    Week52Extreme extreme = Week52Extreme.Both, Week52Mode mode = Week52Mode.NewExtreme, double pullbackPercent = 5) =>
    new() { TimeFrame = TimeFrame.Day, Weeks = 52, Extreme = extreme, Mode = mode, PullbackPercent = pullbackPercent };

  [Fact]
  public async Task Close_AboveYearHigh_TriggersNewHighOnce()
  {
    var (handler, stateSlot) = CreateHandler();

    // 372 consecutive candles: the 364-day calendar window is fully covered on the last one
    var first = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(), Series(371, 106m)), default);

    Assert.True(first.IsTriggered);
    Assert.Equal(Week52AlertKind.NewHigh, first.Kind);
    Assert.Equal((106m / 105m - 1m) * 100m, (decimal)first.Value, 3);

    // The close stays above the old extreme: no repeated alert while the zone is held
    var second = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(), Series(371, 107m)), default);

    Assert.False(second.IsTriggered);
  }

  [Fact]
  public async Task Close_LeavesAndReentersZone_TriggersAgain()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template();

    Assert.True((await handler.Handle(IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, template, Series(371, 106m)), default)).IsTriggered);
    Assert.False((await handler.Handle(IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, template, Series(371, 101m)), default)).IsTriggered);

    var again = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, template, Series(372, 108m)), default);

    // The latch left the zone on 101, so the next step over the window's max high fires again
    Assert.True(again.IsTriggered);
    Assert.Equal(Week52AlertKind.NewHigh, again.Kind);
  }

  [Fact]
  public async Task Close_BelowYearLow_TriggersNewLow()
  {
    var (handler, stateSlot) = CreateHandler();

    // The window lows are 95; a close of 94 is a fresh 52-week low
    var result = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(), Series(371, 94m, lastHigh: 100m)), default);

    Assert.True(result.IsTriggered);
    Assert.Equal(Week52AlertKind.NewLow, result.Kind);
    Assert.True(result.Value < 0);
  }

  [Fact]
  public async Task PullbackFromYearHigh_TriggersWithPullbackKind()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template(mode: Week52Mode.Pullback, pullbackPercent: 5);

    var first = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, template, Series(371, 99.4m, lastHigh: 100m)), default);

    // The window high is 105: 99.4 sits 5.33% under it - past the 5% pullback threshold
    Assert.True(first.IsTriggered);
    Assert.Equal(Week52AlertKind.PullbackFromHigh, first.Kind);
    Assert.Equal(-((105m - 99.4m) / 105m * 100m), (decimal)first.Value, 3);
  }

  [Fact]
  public async Task BounceFromYearLow_TriggersWithBounceKind()
  {
    var (handler, stateSlot) = CreateHandler();
    var template = Template(mode: Week52Mode.Pullback, pullbackPercent: 5);

    // The window lows are 95; a close 5.26% above them enters the bounce zone
    var result = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, template, Series(371, 100m, lastHigh: 100m)), default);

    Assert.True(result.IsTriggered);
    Assert.Equal(Week52AlertKind.BounceFromLow, result.Kind);
    Assert.Equal((100m / 95m - 1m) * 100m, (decimal)result.Value, 3);
  }

  [Fact]
  public async Task ShortHistory_KeepsConditionSilentlyUnmet()
  {
    var (handler, stateSlot) = CreateHandler();

    // 100 candles: the close clears every existing high, but the calendar window is far from
    // covered - reporting "new 52-week high" off three months of history would lie
    var result = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(), Series(99, 106m)), default);

    Assert.False(result.IsTriggered);
  }

  [Fact]
  public async Task ExtremeHigh_IgnoresYearLowBreakout()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(extreme: Week52Extreme.High), Series(371, 94m, lastHigh: 100m)), default);

    Assert.False(result.IsTriggered);
  }

  [Fact]
  public async Task ExtremeLow_IgnoresYearHighBreakout()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(extreme: Week52Extreme.Low), Series(371, 106m)), default);

    Assert.False(result.IsTriggered);
  }

  [Fact]
  public async Task FewerThanTwoCandles_DoesNotThrow()
  {
    var (handler, stateSlot) = CreateHandler();

    var result = await handler.Handle(
      IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlot, Ticker, Template(), [Flat(0, 100m)]), default);

    Assert.False(result.IsTriggered);
  }
}
