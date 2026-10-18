using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Indicators.Tests.Common;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class MaIndicatorTests : IndicatorTestsBase
{
  public MaIndicatorTests(IndicatorCacheFixture indicatorCacheFixture, CandleRepositoryFixture candleRepositoryFixture) : base(indicatorCacheFixture, candleRepositoryFixture)
  {
  }

  [Theory]
  [ClassData(typeof(MaIndicatorClassData))]
  public async Task MovingAverageTest(int periods, double deviation, DateTime start, DateTime end, Validator validator)
  {
    var handler = new EmaIndicatorCommandHandler(IndicatorCache, Logger.None);

    var template = new EmaIndicatorTemplate
    {
      Periods = periods,
      Deviation = deviation
    };

    // One state slot for both replays: the EMA keeps its cross-direction state per slot
    var stateSlot = Guid.NewGuid();

    var candles = await GetAppleCandlesAsync(TimeFrame.Day, start, end);

    var indicatorResult = await handler.Handle(IndicatorCommandBase.Create<EmaIndicatorCommand>(stateSlot, "AAPL", template, candles[..^1]), default);

    Assert.Equal(validator.FirstExpectedResult, Math.Round(indicatorResult.Result, 5));
    Assert.Equal(MaTest.None, indicatorResult.Test);
    Assert.False(indicatorResult.IsTriggered);

    var secondIndicatorResult = await handler.Handle(IndicatorCommandBase.Create<EmaIndicatorCommand>(stateSlot, "AAPL", template, candles), default);

    Assert.Equal(validator.SecondExpectedResult, Math.Round(secondIndicatorResult.Result, 5));
    Assert.Equal(validator.SecondExpectedTest, secondIndicatorResult.Test);
    Assert.True(secondIndicatorResult.IsTriggered);
  }

  [Fact]
  public async Task SmaWithFewerCandlesThanPeriods_DoesNotTriggerOrThrow()
  {
    var handler = new SmaIndicatorCommandHandler(IndicatorCache, Logger.None);

    var candles = await GetAppleCandlesAsync(TimeFrame.Day, new DateTime(2021, 9, 1), new DateTime(2021, 9, 10));

    Assert.True(candles.Length is > 0 and < 50);

    var template = new MaIndicatorTemplate { Periods = 50, Deviation = 0.0005 };

    var indicatorResult = await handler.Handle(IndicatorCommandBase.Create<SmaIndicatorCommand>(Guid.NewGuid(), "AAPL", template, candles), default);

    Assert.False(indicatorResult.IsTriggered);
    Assert.Equal(MaTest.None, indicatorResult.Test);
  }

  [Fact]
  public async Task EmaWithFewerCandlesThanPeriods_DoesNotTriggerOrThrow()
  {
    var handler = new EmaIndicatorCommandHandler(IndicatorCache, Logger.None);

    var candles = await GetAppleCandlesAsync(TimeFrame.Day, new DateTime(2021, 9, 1), new DateTime(2021, 9, 10));

    Assert.True(candles.Length is > 0 and < 50);

    var template = new EmaIndicatorTemplate { Periods = 50, Deviation = 0.0005 };

    var indicatorResult = await handler.Handle(IndicatorCommandBase.Create<EmaIndicatorCommand>(Guid.NewGuid(), "AAPL", template, candles), default);

    Assert.False(indicatorResult.IsTriggered);
    Assert.Equal(MaTest.None, indicatorResult.Test);
  }

  [Fact]
  public async Task EmaPrecisionTest()
  {
    var handler = new EmaIndicatorCommandHandler(IndicatorCache, Logger.None);

    var template = new EmaIndicatorTemplate
    {
      Periods = 5,
      Deviation = 0.01F
    };

    var candles = await GetAppleCandlesAsync(TimeFrame.Day, new DateTime(2022, 3, 24), new DateTime(2022, 4, 14));

    var indicatorResult = await handler.Handle(IndicatorCommandBase.Create<EmaIndicatorCommand>(Guid.NewGuid(), "AAPL", template, candles), default);

    Assert.Equal(Math.Round(168.15959F, 5), Math.Round(indicatorResult.Result, 5));
  }

  public record struct Validator(double FirstExpectedResult, double SecondExpectedResult, MaTest SecondExpectedTest);

  private class MaIndicatorClassData : TheoryData<int, double, DateTime, DateTime, Validator>
  {
    public MaIndicatorClassData()
    {
      // Upward cross: the last candle (2021-10-15) opened at 143.74 below the EMA 144.46946
      // and closed at 144.84 inside the deviation band above it
      Add(50, 0.01F, new DateTime(2021, 6, 1), new DateTime(2021, 10, 15), new Validator(Math.Round(144.45433, 5), Math.Round(144.46946, 5), MaTest.Upward));

      // Downward cross: the last candle (2021-09-17) opened at 147.90 above the EMA 146.29878
      // and closed at 146.06 inside the deviation band below it
      Add(50, 0.01F, new DateTime(2021, 6, 1), new DateTime(2021, 9, 17), new Validator(Math.Round(146.30853, 5), Math.Round(146.29878, 5), MaTest.Downward));
    }
  }
}