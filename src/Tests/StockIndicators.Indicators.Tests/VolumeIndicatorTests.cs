using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Volume;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Templates;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class VolumeIndicatorTests
{
  private const string Ticker = "AAPL";

  private static Candle[] Candles(decimal[] volumes, decimal price = 100m)
  {
    var start = new DateTime(2026, 1, 5, 15, 0, 0, DateTimeKind.Utc);

    return volumes
      .Select((volume, i) => new Candle(Ticker, start.AddMinutes(i), TimeFrame.Minute, price, price, price, price, volume, true, true, "NASDAQ"))
      .ToArray();
  }

  private static Task<VolumeIndicatorResult> RunAsync(VolumeIndicatorTemplate template, decimal[] volumes) =>
    new VolumeIndicatorCommandHandler(Logger.None)
      .Handle(IndicatorCommandBase.Create<VolumeIndicatorCommand>(Guid.NewGuid(), Ticker, template, Candles(volumes)), default);

  [Fact]
  public async Task Spike_AboveMultiplier_Triggers()
  {
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 3, Multiplier = 2 };

    var result = await RunAsync(template, [100m, 100m, 100m, 100m, 250m]);

    Assert.True(result.IsTriggered);
    Assert.Equal(100m, result.Average);
    Assert.Equal(2.5, result.Ratio, 3);
  }

  [Fact]
  public async Task Spike_EqualToMultiplier_Triggers()
  {
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 3, Multiplier = 2 };

    var result = await RunAsync(template, [100m, 100m, 100m, 200m]);

    Assert.True(result.IsTriggered);
  }

  [Fact]
  public async Task SpikeBelowMultiplier_DoesNotTrigger()
  {
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 3, Multiplier = 2 };

    var result = await RunAsync(template, [100m, 100m, 100m, 199m]);

    Assert.False(result.IsTriggered);
  }

  // The current candle must not be part of the average it is compared against,
  // otherwise a huge spike inflates the very baseline that decides the alert.
  [Fact]
  public async Task CurrentVolume_IsExcludedFromTheAverage()
  {
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 3, Multiplier = 2 };

    var result = await RunAsync(template, [100m, 100m, 100m, 1_000m]);

    Assert.Equal(100m, result.Average);
    Assert.Equal(10.0, result.Ratio, 3);
    Assert.True(result.IsTriggered);
  }

  // A warmup-incomplete window is the evaluator's job to skip, but the handler
  // must not divide by zero if a short batch reaches it anyway.
  [Fact]
  public async Task FewerCandlesThanPeriods_DoesNotTriggerOrThrow()
  {
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 20, Multiplier = 2 };

    var result = await RunAsync(template, [500m, 500m]);

    Assert.False(result.IsTriggered);
  }

  // Flat volume history: the average is 0, so nothing can be a multiple of it.
  [Fact]
  public async Task ZeroAverageVolume_DoesNotTriggerOrThrow()
  {
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 3, Multiplier = 2 };

    var result = await RunAsync(template, [0m, 0m, 0m, 500m]);

    Assert.False(result.IsTriggered);
    Assert.Equal(0, result.Ratio);
  }

  // Stateless by design: every spike candle is a signal (the subscription cooldown paces alerts).
  [Fact]
  public async Task RepeatedEvaluation_OfTheSameSpike_StillTriggers()
  {
    var handler = new VolumeIndicatorCommandHandler(Logger.None);
    var template = new VolumeIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 3, Multiplier = 2 };
    var command = IndicatorCommandBase.Create<VolumeIndicatorCommand>(Guid.NewGuid(), Ticker, template, Candles([100m, 100m, 100m, 300m]));

    Assert.True((await handler.Handle(command, default)).IsTriggered);
    Assert.True((await handler.Handle(command, default)).IsTriggered);
  }
}
