using Serilog;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Adx;
using StockIndicators.Application.Indicators.Aroon;
using StockIndicators.Application.Indicators.Atr;
using StockIndicators.Application.Indicators.BollingerBands;
using StockIndicators.Application.Indicators.Cci;
using StockIndicators.Application.Indicators.Chandelier;
using StockIndicators.Application.Indicators.Cmo;
using StockIndicators.Application.Indicators.Donchian;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Application.Indicators.Ichimoku;
using StockIndicators.Application.Indicators.Keltner;
using StockIndicators.Application.Indicators.Macd;
using StockIndicators.Application.Indicators.Mfi;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Indicators.Obv;
using StockIndicators.Application.Indicators.OpenChange;
using StockIndicators.Application.Indicators.PriceChange;
using StockIndicators.Application.Indicators.Psar;
using StockIndicators.Application.Indicators.RoundPrice;
using StockIndicators.Application.Indicators.Rsi;
using StockIndicators.Application.Indicators.StochRsi;
using StockIndicators.Application.Indicators.Stochastic;
using StockIndicators.Application.Indicators.SuperTrend;
using StockIndicators.Application.Indicators.Tsi;
using StockIndicators.Application.Indicators.TrailingStop;
using StockIndicators.Application.Indicators.Volume;
using StockIndicators.Application.Indicators.Vortex;
using StockIndicators.Application.Indicators.Vwap;
using StockIndicators.Application.Indicators.Week52HighLow;
using StockIndicators.Application.Indicators.WilliamsR;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Infrastructure.Domain.Indicators;
using LazyCache;
using Xunit;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;

namespace StockIndicators.Indicators.Tests.Charts;

/// <summary>
/// Guards the embedded example fixtures: every indicator must have a window ending on a
/// candle where the production handler actually triggers at the sample parameters. If a
/// template default changes and an anchor goes stale, this suite fails.
/// </summary>
public class ExampleMarketDataTests
{
  public static readonly TheoryData<AppIndicator, string, TimeFrame> Expectations = new()
  {
    { AppIndicator.SMA, "AAPL", TimeFrame.Day },
    { AppIndicator.EMA, "AAPL", TimeFrame.Day },
    { AppIndicator.RSI, "AAPL", TimeFrame.Day },
    { AppIndicator.MACD, "AAPL", TimeFrame.Day },
    { AppIndicator.BollingerBands, "AAPL", TimeFrame.Day },
    { AppIndicator.SuperTrend, "AAPL", TimeFrame.Day },
    { AppIndicator.ADX, "AAPL", TimeFrame.Day },
    { AppIndicator.Stochastic, "AAPL", TimeFrame.Day },
    { AppIndicator.StochRSI, "AAPL", TimeFrame.Day },
    { AppIndicator.Ichimoku, "AAPL", TimeFrame.Day },
    { AppIndicator.OBV, "AAPL", TimeFrame.Day },
    { AppIndicator.Gap, "TSLA", TimeFrame.Day },
    { AppIndicator.ATR, "TSLA", TimeFrame.Day },
    { AppIndicator.VWAP, "AAPL", TimeFrame.Minute },
    { AppIndicator.RoundPrice, "TSLA", TimeFrame.Minute },
    { AppIndicator.Volume, "AAPL", TimeFrame.Minute },
    { AppIndicator.PriceChange, "TSLA", TimeFrame.Day },
    { AppIndicator.CCI, "AAPL", TimeFrame.Day },
    { AppIndicator.MFI, "AAPL", TimeFrame.Day },
    { AppIndicator.WilliamsR, "AAPL", TimeFrame.Day },
    { AppIndicator.CMO, "AAPL", TimeFrame.Day },
    { AppIndicator.TSI, "AAPL", TimeFrame.Day },
    { AppIndicator.Keltner, "AAPL", TimeFrame.Day },
    { AppIndicator.Donchian, "AAPL", TimeFrame.Day },
    { AppIndicator.WMA, "AAPL", TimeFrame.Day },
    { AppIndicator.HMA, "AAPL", TimeFrame.Day },
    { AppIndicator.VWMA, "AAPL", TimeFrame.Day },
    { AppIndicator.KAMA, "TSLA", TimeFrame.Day },
    { AppIndicator.PSAR, "TSLA", TimeFrame.Day },
    { AppIndicator.Chandelier, "TSLA", TimeFrame.Day },
    { AppIndicator.Aroon, "TSLA", TimeFrame.Day },
    { AppIndicator.Vortex, "TSLA", TimeFrame.Day },
    { AppIndicator.OpenChange, "AAPL", TimeFrame.Day },
    { AppIndicator.Week52HighLow, "AAPL", TimeFrame.Day },
    { AppIndicator.TrailingStop, "TSLA", TimeFrame.Day },
  };

  [Theory]
  [MemberData(nameof(Expectations))]
  public async Task ExampleWindow_EndsOnTriggeringCandle(AppIndicator indicator, string ticker, TimeFrame timeFrame)
  {
    Assert.True(ExampleMarketData.TryGetWindow(indicator, out var window), $"{indicator}: no example window");

    Assert.Equal(ticker, window.Ticker);
    Assert.Equal(timeFrame, window.TimeFrame);
    Assert.Equal(window.Candles[^1].Timestamp, window.TriggerDate);
    Assert.True(window.Candles.Length >= 2, $"{indicator}: window too short");

    var (triggered, _) = await ReplayToEndTriggers(indicator, window.Candles);

    Assert.True(triggered, $"{indicator}: the handler does not trigger on the window's last candle");
  }

  /// <summary>
  /// The trigger marker direction on the example chart must agree with what the production
  /// handler's typed result says about the fired signal (its trend/position fields), so the
  /// arrow on the docs picture can never read as the opposite of the alert semantics.
  /// </summary>
  [Theory]
  [MemberData(nameof(Expectations))]
  public async Task ExampleTriggerDirection_MatchesHandlerResult(AppIndicator indicator, string ticker, TimeFrame timeFrame)
  {
    Assert.True(ExampleMarketData.TryGetWindow(indicator, out var window), $"{indicator} ({ticker} {timeFrame}): no example window");

    var (triggered, lastResult) = await ReplayToEndTriggers(indicator, window.Candles);

    Assert.True(triggered, $"{indicator} ({ticker} {timeFrame}): the handler does not trigger on the window's last candle");

    var template = (IIndicatorTemplate)Activator.CreateInstance(indicator.GetTemplate())!;
    ExampleIndicatorChart.ApplySampleDefaults(template);

    var built = IndicatorChartBuilder.Build(indicator, template, window.Candles);
    var resolved = TriggerDirectionResolver.Resolve(indicator, template, window.Candles, built.Series, window.TriggerDate);

    if (indicator == AppIndicator.StochRSI)
    {
      // Its result carries no cross direction; the zone filter guarantees the fired cross
      // left a channel, so the resolver must attribute up or down. A bullish cross ends
      // with %K above %D, a bearish one below.
      var cross = (StochRsiIndicatorResult)lastResult.Current;

      Assert.Equal(cross.PercK > cross.PercD ? ChartTriggerDirection.Up : ChartTriggerDirection.Down, resolved);
      return;
    }

    var expected = ExpectedDirection(indicator, lastResult, window.Candles);

    Assert.NotNull(expected);
    Assert.Equal(expected, resolved);
  }

  /// <summary>
  /// The direction the handler's typed result attributes to its signal. Indicators whose
  /// trigger is a directionless event are neutral by definition; RSI is derived from the
  /// transition between the previous and current step state (its result has no direction);
  /// StochRSI's result carries no cross direction, so only "up or down" is asserted there.
  /// </summary>
  private static ChartTriggerDirection? ExpectedDirection(AppIndicator indicator, ReplayStep step, Candle[] candles) =>
    indicator switch
    {
      AppIndicator.ADX or AppIndicator.ATR or AppIndicator.Volume or AppIndicator.RoundPrice => ChartTriggerDirection.Neutral,
      AppIndicator.SMA or AppIndicator.EMA or AppIndicator.WMA or AppIndicator.HMA or AppIndicator.VWMA or AppIndicator.KAMA
        => ((MaIndicatorResult)step.Current).Test == MaTest.Upward
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.CCI or AppIndicator.MFI or AppIndicator.WilliamsR or AppIndicator.CMO => ExpectedZone(step),
      AppIndicator.RSI => ((RsiIndicatorResult)step.Previous).State == RsiState.Overbought || ((RsiIndicatorResult)step.Current).State == RsiState.Oversold
        ? ChartTriggerDirection.Down
        : ChartTriggerDirection.Up,
      AppIndicator.MACD => ((MacdIndicatorResult)step.Current).Trend == MacdTrend.Bullish
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.BollingerBands => ExpectedBollinger((BollingerBandsIndicatorResult)step.Previous, (BollingerBandsIndicatorResult)step.Current),
      AppIndicator.SuperTrend => ((SuperTrendIndicatorResult)step.Current).Direction == SuperTrendDirection.Bullish
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Stochastic => ((StochasticIndicatorResult)step.Current).Trend == StochTrend.BullishCross
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.StochRSI => null,
      AppIndicator.Ichimoku => ((IchimokuIndicatorResult)step.Current).Position == IchimokuPosition.TKCrossBullish
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.OBV => ((ObvIndicatorResult)step.Current).Trend == ObvTrend.Bullish
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.VWAP => ((VwapIndicatorResult)step.Current).Position == VwapPosition.Above
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Gap => candles[^1].Open > candles[^2].Close
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.PriceChange => ((PriceChangeIndicatorResult)step.Current).Change >= 0
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.TSI => ((Application.Indicators.Tsi.TsiIndicatorResult)step.Current).Trend == Application.Indicators.Tsi.TsiTrend.Bullish
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Keltner => ExpectedChannel(
        ((Application.Indicators.Keltner.KeltnerIndicatorResult)step.Previous).Position,
        ((Application.Indicators.Keltner.KeltnerIndicatorResult)step.Current).Position),
      AppIndicator.Donchian => ExpectedChannel(
        ((Application.Indicators.Donchian.DonchianIndicatorResult)step.Previous).Position,
        ((Application.Indicators.Donchian.DonchianIndicatorResult)step.Current).Position),
      AppIndicator.PSAR => ((Application.Indicators.Psar.PsarIndicatorResult)step.Current).Direction == Application.Indicators.Psar.PsarDirection.Bullish
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Chandelier => ((Application.Indicators.Chandelier.ChandelierIndicatorResult)step.Current).Side == Application.Indicators.Chandelier.ChandelierSide.Above
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Aroon => ((Application.Indicators.Aroon.AroonIndicatorResult)step.Current).Trend == Application.Indicators.Aroon.AroonTrend.BullishCross
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Vortex => ((Application.Indicators.Vortex.VortexIndicatorResult)step.Current).Trend == Application.Indicators.Vortex.VortexTrend.BullishCross
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.OpenChange => ((Application.Indicators.OpenChange.OpenChangeIndicatorResult)step.Current).Change >= 0
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      AppIndicator.Week52HighLow => ((Application.Indicators.Week52HighLow.Week52HighLowIndicatorResult)step.Current).Kind
        is Application.Indicators.Week52HighLow.Week52AlertKind.NewHigh
        or Application.Indicators.Week52HighLow.Week52AlertKind.BounceFromLow
        ? ChartTriggerDirection.Up
        : ChartTriggerDirection.Down,
      // Only the crossing into the unsafe side alerts: a long stop hit is a sell, a short one a buy.
      AppIndicator.TrailingStop => ((Application.Indicators.TrailingStop.TrailingStopIndicatorResult)step.Current).Side
        == Application.Indicators.TrailingStop.TrailingSide.Below
        ? ChartTriggerDirection.Down
        : ChartTriggerDirection.Up,
      _ => null,
    };

  // The RSI-shaped zone transition on the typed results: leaving the overbought zone or
  // entering the oversold one is a down signal, the mirrored moves an up one.
  private static ChartTriggerDirection ExpectedZone(ReplayStep step)
  {
    var previousState = (ZoneState?)step.Previous.GetType().GetProperty("State")?.GetValue(step.Previous);
    var currentState = (ZoneState?)step.Current.GetType().GetProperty("State")?.GetValue(step.Current);

    return previousState == ZoneState.Overbought || currentState == ZoneState.Oversold
      ? ChartTriggerDirection.Down
      : ChartTriggerDirection.Up;
  }

  private static ChartTriggerDirection ExpectedChannel(ChannelPosition previous, ChannelPosition current)
  {
    var before = previous == ChannelPosition.Inside ? 0 : previous == ChannelPosition.AboveUpper ? 1 : -1;
    var now = current == ChannelPosition.Inside ? 0 : current == ChannelPosition.AboveUpper ? 1 : -1;

    if (before == 0)
      return now == 1 ? ChartTriggerDirection.Up : ChartTriggerDirection.Down;

    return before == 1 ? ChartTriggerDirection.Down : ChartTriggerDirection.Up;
  }

  private static ChartTriggerDirection? ExpectedBollinger(BollingerBandsIndicatorResult previous, BollingerBandsIndicatorResult current)
  {
    var before = previous.Position == BbPosition.Inside ? 0 : previous.Position is BbPosition.AboveUpper or BbPosition.TouchingUpper ? 1 : -1;
    var now = current.Position == BbPosition.Inside ? 0 : current.Position is BbPosition.AboveUpper or BbPosition.TouchingUpper ? 1 : -1;

    if (before == 0)
      return now == 1 ? ChartTriggerDirection.Up : ChartTriggerDirection.Down;

    return before == 1 ? ChartTriggerDirection.Down : ChartTriggerDirection.Up;
  }

  private sealed record ReplayStep(IndicatorResult Previous, IndicatorResult Current);

  /// <summary>
  /// Replays the production handler over the window exactly the way the anchor tool did:
  /// a growing window for series indicators, the last two candles for Gap, the last candle
  /// for RoundPrice, session-aligned growth for VWAP. Returns whether the final step
  /// triggered and the previous/current typed results of that final step.
  /// </summary>
  private static async Task<(bool Triggered, ReplayStep Step)> ReplayToEndTriggers(AppIndicator indicator, Candle[] candles)
  {
    var template = (IIndicatorTemplate)Activator.CreateInstance(indicator.GetTemplate())!;
    ExampleIndicatorChart.ApplySampleDefaults(template);

    var evaluate = Evaluator(indicator, candles, template);

    var last = false;
    IndicatorResult previous = null!;

    if (indicator == AppIndicator.Gap)
    {
      var result = await evaluate(candles[^2..]);
      return (result.IsTriggered, new ReplayStep(result, result));
    }

    if (indicator == AppIndicator.RoundPrice)
    {
      var result = await evaluate([candles[^1]]);
      return (result.IsTriggered, new ReplayStep(result, result));
    }

    var start = 0;
    IndicatorResult current = null!;

    // VWAP is session VWAP: its window starts at the first candle of the session, which is
    // exactly where the fixture window begins, so the growing replay starts there too.
    for (var i = start + 1; i <= candles.Length; i++)
    {
      // The handlers mutate their cached result object in place, so the previous step's
      // state has to be snapshotted before the next evaluation overwrites it.
      previous = current is null ? null! : Snapshot(current);
      current = await evaluate(candles[start..i]);
      last = current.IsTriggered;
    }

    return (last, new ReplayStep(previous, current));
  }

  private static IndicatorResult Snapshot(IndicatorResult result)
  {
    var type = result.GetType();
    var copy = (IndicatorResult)Activator.CreateInstance(type)!;

    foreach (var property in type.GetProperties().Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0))
      property.SetValue(copy, property.GetValue(result));

    return copy;
  }

  private static Func<Candle[], Task<IndicatorResult>> Evaluator(AppIndicator indicator, Candle[] candles, IIndicatorTemplate template)
  {
    IIndicatorCache cache = new IndicatorInMemoryCache(new CachingService());

    // One state slot for the whole growing replay (the old per-subscription slot), so the
    // stateful handlers keep their cross/gap state across the window steps
    var stateSlot = Guid.NewGuid();
    var ticker = candles[0].Ticker;

    ILogger logger = Logger.None;

    return indicator switch
    {
      AppIndicator.SMA => async c => await new SmaIndicatorCommandHandler(cache, logger).Handle(Create<SmaIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.EMA => async c => await new EmaIndicatorCommandHandler(cache, logger).Handle(Create<EmaIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.RSI => async c => await new RsiIndicatorCommandHandler(cache, logger).Handle(Create<RsiIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.MACD => async c => await new MacdIndicatorCommandHandler(cache, logger).Handle(Create<MacdIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.BollingerBands => async c => await new BollingerBandsIndicatorCommandHandler(cache, logger).Handle(Create<BollingerBandsIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.SuperTrend => async c => await new SuperTrendIndicatorCommandHandler(cache, logger).Handle(Create<SuperTrendIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.ADX => async c => await new AdxIndicatorCommandHandler(cache, logger).Handle(Create<AdxIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.ATR => async c => await new AtrIndicatorCommandHandler(cache, logger).Handle(Create<AtrIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.OBV => async c => await new ObvIndicatorCommandHandler(cache, logger).Handle(Create<ObvIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Stochastic => async c => await new StochasticIndicatorCommandHandler(cache, logger).Handle(Create<StochasticIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.StochRSI => async c => await new StochRsiIndicatorCommandHandler(cache, logger).Handle(Create<StochRsiIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Ichimoku => async c => await new IchimokuIndicatorCommandHandler(cache, logger).Handle(Create<IchimokuIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.VWAP => async c => await new VwapIndicatorCommandHandler(cache, logger).Handle(Create<VwapIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Gap => async c => await new GapIndicatorCommandHandler(cache, logger).Handle(Create<GapIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.RoundPrice => async c => await new RoundPriceIndicatorCommandHandler(logger).Handle(Create<RoundPriceIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Volume => async c => await new VolumeIndicatorCommandHandler(logger).Handle(Create<VolumeIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.PriceChange => async c => await new PriceChangeIndicatorCommandHandler(cache, logger).Handle(Create<PriceChangeIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.CCI => async c => await new CciIndicatorCommandHandler(cache, logger).Handle(Create<CciIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.MFI => async c => await new MfiIndicatorCommandHandler(cache, logger).Handle(Create<MfiIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.WilliamsR => async c => await new WilliamsRIndicatorCommandHandler(cache, logger).Handle(Create<WilliamsRIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.CMO => async c => await new CmoIndicatorCommandHandler(cache, logger).Handle(Create<CmoIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.TSI => async c => await new TsiIndicatorCommandHandler(cache, logger).Handle(Create<TsiIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Keltner => async c => await new KeltnerIndicatorCommandHandler(cache, logger).Handle(Create<KeltnerIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Donchian => async c => await new DonchianIndicatorCommandHandler(cache, logger).Handle(Create<DonchianIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.WMA => async c => await new WmaIndicatorCommandHandler(cache, logger).Handle(Create<WmaIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.HMA => async c => await new HmaIndicatorCommandHandler(cache, logger).Handle(Create<HmaIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.VWMA => async c => await new VwmaIndicatorCommandHandler(cache, logger).Handle(Create<VwmaIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.KAMA => async c => await new KamaIndicatorCommandHandler(cache, logger).Handle(Create<KamaIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.PSAR => async c => await new PsarIndicatorCommandHandler(cache, logger).Handle(Create<PsarIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Chandelier => async c => await new ChandelierIndicatorCommandHandler(cache, logger).Handle(Create<ChandelierIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Aroon => async c => await new AroonIndicatorCommandHandler(cache, logger).Handle(Create<AroonIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Vortex => async c => await new VortexIndicatorCommandHandler(cache, logger).Handle(Create<VortexIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.OpenChange => async c => await new OpenChangeIndicatorCommandHandler(cache, logger).Handle(Create<OpenChangeIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.Week52HighLow => async c => await new Week52HighLowIndicatorCommandHandler(cache, logger).Handle(Create<Week52HighLowIndicatorCommand>(stateSlot, ticker, template, c), default),
      AppIndicator.TrailingStop => async c => await new TrailingStopIndicatorCommandHandler(cache, logger).Handle(Create<TrailingStopIndicatorCommand>(stateSlot, ticker, template, c), default),
      _ => throw new ArgumentOutOfRangeException(nameof(indicator)),
    };
  }

  private static TCommand Create<TCommand>(Guid stateSlot, string ticker, IIndicatorTemplate template, Candle[] candles) where TCommand : IndicatorCommandBase, new() =>
    IndicatorCommandBase.Create<TCommand>(stateSlot, ticker, template, candles);
}
