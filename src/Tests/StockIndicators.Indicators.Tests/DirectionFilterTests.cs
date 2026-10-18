using LazyCache;
using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.BollingerBands;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Application.Indicators.Macd;
using StockIndicators.Application.Indicators.Rsi;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The shared AlertDirection filter of the RSI, MACD, BollingerBands and Gap handlers:
/// Both keeps the historical both-sides behavior, Up/Down watch only their own side.
/// Stateful handlers return the same cached result object from every run, so the warmup
/// values are captured before the evaluated run mutates them.
/// </summary>
public class DirectionFilterTests
{
  private const string Ticker = "TEST";
  private static readonly DateTime Start = new(2026, 1, 5, 15, 0, 0, DateTimeKind.Utc);

  private static Candle Flat(int index, decimal close, decimal open) =>
    new(Ticker, Start.AddMinutes(index), TimeFrame.Minute, open, close, close, close, 1_000m, true, true, "NASDAQ");

  private static IndicatorInMemoryCache NewCache() => new(new CachingService());

  // Strictly rising closes: gains only, RSI(14) saturates at 100.
  private static List<Candle> RisingCandles(int count) =>
    Enumerable.Range(0, count).Select(i => Flat(i, 100m * (1.01m + 0.01m * i), 100m)).ToList();

  // Alternating +0.5/-2 closes: RSI(14) sits in the oversold zone.
  private static List<Candle> SinkingCandles(int count)
  {
    var price = 100m;
    var candles = new List<Candle>();

    for (var i = 0; i < count; i++)
    {
      price += i % 2 == 0 ? 0.5m : -2m;
      candles.Add(Flat(i, price, candles.Count == 0 ? 100m : candles[^1].Close));
    }

    return candles;
  }

  private static RsiIndicatorTemplate RsiTemplate(AlertDirection direction) =>
    new() { TimeFrame = TimeFrame.Minute, Period = 14, Overbought = 70, Oversold = 30, Direction = direction };

  [Theory]
  [InlineData(AlertDirection.Up, true)]
  [InlineData(AlertDirection.Down, false)]
  [InlineData(AlertDirection.Both, true)]
  public async Task Rsi_LeavingOverbought_FiresUpZoneOnly(AlertDirection direction, bool expected)
  {
    var handler = new RsiIndicatorCommandHandler(NewCache(), Logger.None);
    var stateSlot = Guid.NewGuid();
    var template = RsiTemplate(direction);

    var warm = RisingCandles(30);
    var warmup = await handler.Handle(IndicatorCommandBase.Create<RsiIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);
    var warmRsi = warmup.Rsi;
    var warmTriggered = warmup.IsTriggered;

    // A single crash candle drops RSI out of the overbought zone: that is the overbought zone's event.
    warm.Add(Flat(30, 100m, warm[^1].Close));
    var result = await handler.Handle(IndicatorCommandBase.Create<RsiIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);

    Assert.False(warmTriggered);
    Assert.True(warmRsi >= 70);
    Assert.Equal(expected, result.IsTriggered);
  }

  [Theory]
  [InlineData(AlertDirection.Up, false)]
  [InlineData(AlertDirection.Down, true)]
  [InlineData(AlertDirection.Both, true)]
  public async Task Rsi_LeavingOversold_FiresDownZoneOnly(AlertDirection direction, bool expected)
  {
    var handler = new RsiIndicatorCommandHandler(NewCache(), Logger.None);
    var stateSlot = Guid.NewGuid();
    var template = RsiTemplate(direction);

    var warm = SinkingCandles(30);
    var warmup = await handler.Handle(IndicatorCommandBase.Create<RsiIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);
    var warmRsi = warmup.Rsi;
    var warmTriggered = warmup.IsTriggered;

    // A +20 rally candle pulls RSI back to neutral: that is the oversold zone's event.
    warm.Add(Flat(30, warm[^1].Close + 20m, warm[^1].Close));
    var result = await handler.Handle(IndicatorCommandBase.Create<RsiIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);

    Assert.False(warmTriggered);
    Assert.True(warmRsi <= 30);
    Assert.Equal(expected, result.IsTriggered);
  }

  // The MACD histogram of these synthetic exponential series sits slightly positive in a steady
  // trend and turns clearly negative after a sharp pullback; a strong rally flips it back positive.
  private static List<Candle> ExponentialSeries(int count, decimal rate, List<Candle>? seed = null)
  {
    var candles = seed ?? [];
    var price = candles.Count == 0 ? 100m : candles[^1].Close;
    var startIndex = candles.Count;

    for (var i = startIndex; i < startIndex + count; i++)
    {
      price *= rate;
      candles.Add(Flat(i, price, candles.Count == 0 ? 100m : candles[^1].Close));
    }

    return candles;
  }

  private static MacdIndicatorTemplate MacdTemplate(AlertDirection direction) =>
    new() { TimeFrame = TimeFrame.Minute, FastPeriods = 12, SlowPeriods = 26, SignalPeriods = 9, Direction = direction };

  [Theory]
  [InlineData(AlertDirection.Up, true)]
  [InlineData(AlertDirection.Down, false)]
  [InlineData(AlertDirection.Both, true)]
  public async Task Macd_HistogramFlippingPositive_FiresUpOnly(AlertDirection direction, bool expected)
  {
    var handler = new MacdIndicatorCommandHandler(NewCache(), Logger.None);
    var stateSlot = Guid.NewGuid();
    var template = MacdTemplate(direction);

    // A steady rise followed by a three-candle pullback leaves the histogram negative.
    var warm = ExponentialSeries(60, 1.01m);
    ExponentialSeries(3, 0.95m, warm);
    var warmup = await handler.Handle(IndicatorCommandBase.Create<MacdIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);
    var warmHistogram = warmup.Histogram;
    var warmTriggered = warmup.IsTriggered;

    // Eight rally candles flip the histogram positive: the bullish event.
    ExponentialSeries(8, 1.03m, warm);
    var result = await handler.Handle(IndicatorCommandBase.Create<MacdIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);

    Assert.False(warmTriggered);
    Assert.True(warmHistogram < 0);
    Assert.Equal(expected, result.IsTriggered);
  }

  [Theory]
  [InlineData(AlertDirection.Up, false)]
  [InlineData(AlertDirection.Down, true)]
  [InlineData(AlertDirection.Both, true)]
  public async Task Macd_HistogramFlippingNegative_FiresDownOnly(AlertDirection direction, bool expected)
  {
    var handler = new MacdIndicatorCommandHandler(NewCache(), Logger.None);
    var stateSlot = Guid.NewGuid();
    var template = MacdTemplate(direction);

    var warm = ExponentialSeries(60, 1.01m);
    var warmup = await handler.Handle(IndicatorCommandBase.Create<MacdIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);
    var warmHistogram = warmup.Histogram;
    var warmTriggered = warmup.IsTriggered;

    // Three sharp down candles flip the histogram negative: the bearish event.
    ExponentialSeries(3, 0.97m, warm);
    var result = await handler.Handle(IndicatorCommandBase.Create<MacdIndicatorCommand>(stateSlot, Ticker, template, warm.ToArray()), default);

    Assert.False(warmTriggered);
    Assert.True(warmHistogram > 0);
    Assert.Equal(expected, result.IsTriggered);
  }

  // Flat closes with the final candle beyond a band: the previous candle (the transition partner)
  // is inside, so the event belongs to the side of the band that was crossed.
  private static Candle[] FlatThenJump(decimal lastClose)
  {
    var candles = Enumerable.Range(0, 20).Select(i => Flat(i, 100m, 100m)).ToList();
    candles.Add(Flat(20, lastClose, 100m));
    return candles.ToArray();
  }

  // Flat closes with the second-to-last candle beyond a band and the last one back inside.
  private static Candle[] FlatWithBandReturn(decimal bandClose)
  {
    var candles = Enumerable.Range(0, 19).Select(i => Flat(i, 100m, 100m)).ToList();
    candles.Add(Flat(19, bandClose, 100m));
    candles.Add(Flat(20, 100m, bandClose));
    return candles.ToArray();
  }

  private static BollingerBandsIndicatorTemplate BbTemplate(AlertDirection direction) =>
    new() { TimeFrame = TimeFrame.Minute, Period = 20, StandardDeviation = 2, CandlePart = CandlePart.Close, Direction = direction };

  [Theory]
  [InlineData(AlertDirection.Up, true)]
  [InlineData(AlertDirection.Down, false)]
  [InlineData(AlertDirection.Both, true)]
  public async Task BollingerBands_BreakingUpper_FiresUpOnly(AlertDirection direction, bool expected)
  {
    var handler = new BollingerBandsIndicatorCommandHandler(NewCache(), Logger.None);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<BollingerBandsIndicatorCommand>(Guid.NewGuid(), Ticker, BbTemplate(direction), FlatThenJump(105m)), default);

    Assert.Equal(BbPosition.AboveUpper, result.Position);
    Assert.Equal(expected, result.IsTriggered);
  }

  [Theory]
  [InlineData(AlertDirection.Up, false)]
  [InlineData(AlertDirection.Down, true)]
  [InlineData(AlertDirection.Both, true)]
  public async Task BollingerBands_BreakingLower_FiresDownOnly(AlertDirection direction, bool expected)
  {
    var handler = new BollingerBandsIndicatorCommandHandler(NewCache(), Logger.None);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<BollingerBandsIndicatorCommand>(Guid.NewGuid(), Ticker, BbTemplate(direction), FlatThenJump(95m)), default);

    Assert.Equal(BbPosition.BelowLower, result.Position);
    Assert.Equal(expected, result.IsTriggered);
  }

  [Theory]
  [InlineData(AlertDirection.Up, true)]
  [InlineData(AlertDirection.Down, false)]
  [InlineData(AlertDirection.Both, true)]
  public async Task BollingerBands_ReturningFromUpper_FiresUpOnly(AlertDirection direction, bool expected)
  {
    var handler = new BollingerBandsIndicatorCommandHandler(NewCache(), Logger.None);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<BollingerBandsIndicatorCommand>(Guid.NewGuid(), Ticker, BbTemplate(direction), FlatWithBandReturn(105m)), default);

    Assert.Equal(BbPosition.Inside, result.Position);
    Assert.Equal(expected, result.IsTriggered);
  }

  [Theory]
  [InlineData(AlertDirection.Up, false)]
  [InlineData(AlertDirection.Down, true)]
  [InlineData(AlertDirection.Both, true)]
  public async Task BollingerBands_ReturningFromLower_FiresDownOnly(AlertDirection direction, bool expected)
  {
    var handler = new BollingerBandsIndicatorCommandHandler(NewCache(), Logger.None);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<BollingerBandsIndicatorCommand>(Guid.NewGuid(), Ticker, BbTemplate(direction), FlatWithBandReturn(95m)), default);

    Assert.Equal(BbPosition.Inside, result.Position);
    Assert.Equal(expected, result.IsTriggered);
  }

  // Two-candle fixtures: the gap side is decided by the open against the previous close.
  private static Candle[] GapCandles(decimal previousClose, decimal currentOpen) =>
  [
    new(Ticker, Start, TimeFrame.Minute, previousClose, previousClose, previousClose, previousClose, 1_000m, true, true, "NASDAQ"),
    new(Ticker, Start.AddMinutes(1), TimeFrame.Minute, currentOpen, currentOpen, currentOpen, currentOpen, 1_000m, true, true, "NASDAQ"),
  ];

  private static GapIndicatorTemplate GapTemplate(AlertDirection direction) =>
    new() { TimeFrame = TimeFrame.Minute, Percent = 3, Direction = direction };

  [Theory]
  [InlineData(AlertDirection.Up, true)]
  [InlineData(AlertDirection.Down, false)]
  [InlineData(AlertDirection.Both, true)]
  public async Task Gap_Up_FiresUpOnly(AlertDirection direction, bool expected)
  {
    var handler = new GapIndicatorCommandHandler(NewCache(), Logger.None);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<GapIndicatorCommand>(Guid.NewGuid(), Ticker, GapTemplate(direction), GapCandles(100m, 104m)), default);

    Assert.Equal(expected, result.IsTriggered);
  }

  [Theory]
  [InlineData(AlertDirection.Up, false)]
  [InlineData(AlertDirection.Down, true)]
  [InlineData(AlertDirection.Both, true)]
  public async Task Gap_Down_FiresDownOnly(AlertDirection direction, bool expected)
  {
    var handler = new GapIndicatorCommandHandler(NewCache(), Logger.None);

    var result = await handler.Handle(
      IndicatorCommandBase.Create<GapIndicatorCommand>(Guid.NewGuid(), Ticker, GapTemplate(direction), GapCandles(100m, 96m)), default);

    Assert.Equal(expected, result.IsTriggered);
  }
}
