using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Aroon;
using StockIndicators.Application.Indicators.Cci;
using StockIndicators.Application.Indicators.Chandelier;
using StockIndicators.Application.Indicators.Cmo;
using StockIndicators.Application.Indicators.Donchian;
using StockIndicators.Application.Indicators.Keltner;
using StockIndicators.Application.Indicators.Mfi;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Indicators.Psar;
using StockIndicators.Application.Indicators.Tsi;
using StockIndicators.Application.Indicators.Vortex;
using StockIndicators.Application.Indicators.WilliamsR;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Indicators.Tests.Common;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// Wiring tests for the indicators added on top of the original set: each handler must
/// produce values once its warmup is covered and fire at least once on the deterministic
/// example series (thresholds are relaxed where the seeded walk is too tame to reach the
/// conventional levels).
/// </summary>
public class NewIndicatorTests : IndicatorTestsBase
{
  public NewIndicatorTests(IndicatorCacheFixture indicatorCacheFixture, CandleRepositoryFixture candleRepositoryFixture) : base(indicatorCacheFixture, candleRepositoryFixture)
  {
  }

  private const int CandleCount = 260;

  private static Candle[] Example(Indicator indicator) => ExampleCandles.Generate(indicator, TimeFrame.Day, CandleCount);

  [Fact]
  public async Task Cci_ComputesAndFiresOnZoneChange()
  {
    var template = new CciIndicatorTemplate { Period = 20, Overbought = 20, Oversold = -20 };
    var fired = await SweepAsync(new CciIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.CCI), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Mfi_ComputesAndFiresOnZoneChange()
  {
    var template = new MfiIndicatorTemplate { Period = 14, Overbought = 60, Oversold = 40 };
    var fired = await SweepAsync(new MfiIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.MFI), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task WilliamsR_FiresOnNegativeScaleThresholds()
  {
    var template = new WilliamsRIndicatorTemplate { Period = 14, Overbought = -10, Oversold = -90 };
    Assert.Empty(IndicatorTemplateValidation.GetErrors(template));

    var fired = await SweepAsync(new WilliamsRIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.WilliamsR), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Cmo_ComputesAndFiresOnZoneChange()
  {
    var template = new CmoIndicatorTemplate { Period = 20, Overbought = 30, Oversold = -30 };
    var fired = await SweepAsync(new CmoIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.CMO), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Tsi_FiresOnSignalCrossover()
  {
    var template = new TsiIndicatorTemplate { LongPeriods = 25, SignalPeriods = 13 };
    var candles = await GetAppleCandlesAsync(TimeFrame.Day, new DateTime(2021, 1, 4), new DateTime(2022, 12, 30));
    var fired = await SweepAsync(new TsiIndicatorCommandHandler(IndicatorCache, Logger.None), candles, template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Keltner_FiresOnChannelExit()
  {
    var template = new KeltnerIndicatorTemplate { Period = 20, AtrPeriod = 10, Multiplier = 1.5 };
    var fired = await SweepAsync(new KeltnerIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.Keltner), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Donchian_FiresOnChannelExit()
  {
    var template = new DonchianIndicatorTemplate { Period = 20 };
    var fired = await SweepAsync(new DonchianIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.Donchian), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Wma_FiresOnCrossover()
  {
    var template = new WmaIndicatorTemplate { Periods = 20, Deviation = 0.1 };
    var fired = await SweepAsync(new WmaIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.WMA), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Hma_FiresOnCrossover()
  {
    var template = new HmaIndicatorTemplate { Periods = 20, Deviation = 0.1 };
    var fired = await SweepAsync(new HmaIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.HMA), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Vwma_FiresOnCrossover()
  {
    var template = new VwmaIndicatorTemplate { Periods = 20, Deviation = 0.1 };
    var fired = await SweepAsync(new VwmaIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.VWMA), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Kama_FiresOnCrossover()
  {
    var template = new KamaIndicatorTemplate { ErPeriods = 20, FastPeriods = 2, SlowPeriods = 30, Deviation = 0.1 };
    var fired = await SweepAsync(new KamaIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.KAMA), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Psar_FiresOnTrendReversal()
  {
    var template = new PsarIndicatorTemplate { AccelerationStep = 0.02, MaxAccelerationFactor = 0.2 };
    var fired = await SweepAsync(new PsarIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.PSAR), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Chandelier_FiresOnStopCross()
  {
    var template = new ChandelierIndicatorTemplate { Period = 22, Multiplier = 3, Type = ChandelierDirection.Long };
    var fired = await SweepAsync(new ChandelierIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.Chandelier), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Aroon_FiresOnUpDownCross()
  {
    var template = new AroonIndicatorTemplate { Period = 25 };
    var fired = await SweepAsync(new AroonIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.Aroon), template);

    Assert.True(fired);
  }

  [Fact]
  public async Task Vortex_FiresOnViCross()
  {
    var template = new VortexIndicatorTemplate { Period = 14 };
    var fired = await SweepAsync(new VortexIndicatorCommandHandler(IndicatorCache, Logger.None), Example(Indicator.Vortex), template);

    Assert.True(fired);
  }

  [Fact]
  public void NegativeThresholdsSkipThePositiveCheck()
  {
    // CCI keeps its positive overbought bound checked, the negative oversold one is free.
    var cci = new CciIndicatorTemplate { Period = 20, Overbought = 100, Oversold = -100 };
    Assert.Empty(IndicatorTemplateValidation.GetErrors(cci));

    cci.Oversold = -500;
    Assert.Empty(IndicatorTemplateValidation.GetErrors(cci));

    cci.Overbought = 0;
    Assert.Contains(IndicatorTemplateValidation.GetErrors(cci), error => error.Code == "template.paramPositive");

    // Williams %R lives entirely on the negative scale.
    var williams = new WilliamsRIndicatorTemplate { Period = 14, Overbought = -20, Oversold = -80 };
    Assert.Empty(IndicatorTemplateValidation.GetErrors(williams));

    williams.Period = 0;
    Assert.Contains(IndicatorTemplateValidation.GetErrors(williams), error => error.Code == "template.paramMin");
  }

  /// <summary>
  /// Replays the indicator over the growing example series with one stable state slot
  /// (the same slot the condition id would provide in production).
  /// </summary>
  private static async Task<bool> SweepAsync<TCommand, TResult>(
    StatefulIndicatorCommandHandlerBase<TCommand, TResult> handler,
    Candle[] candles,
    IIndicatorTemplate template)
    where TCommand : IndicatorCommandBase<TResult>, new()
    where TResult : IndicatorResult, new()
  {
    var start = template is ICandleCountDependentIndicatorTemplate candleDependent
      ? candleDependent.GetCandleCount()
      : 50;

    var stateSlot = Guid.NewGuid();

    for (var i = start; i <= candles.Length; i++)
    {
      var result = await handler.Handle(
        IndicatorCommandBase.Create<TCommand>(stateSlot, "TEST", template, candles[..i]), default);

      if (result.IsTriggered)
        return true;
    }

    return false;
  }
}
