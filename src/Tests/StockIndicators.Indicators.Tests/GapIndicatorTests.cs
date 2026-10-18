using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Indicators.Tests.Common;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class GapIndicatorTests : IndicatorTestsBase
{
  public GapIndicatorTests(IndicatorCacheFixture indicatorCacheFixture, CandleRepositoryFixture candleRepositoryFixture) : base(indicatorCacheFixture, candleRepositoryFixture)
  {
  }

  [Theory]
  [ClassData(typeof(ClassData))]
  public async Task GapIndicatorTest(
    double percent,
    DateTime start,
    DateTime end,
    double expected
  )
  {
    var handler = new GapIndicatorCommandHandler(IndicatorCache, Logger.None);

    var template = new GapIndicatorTemplate
    {
      Percent = percent,
      TimeFrame = TimeFrame.Day
    };

    // One state slot for both runs: the second run must see the gap already consumed
    var stateSlot = Guid.NewGuid();

    var candle = await GetAppleCandlesAsync(TimeFrame.Day, start, end);

    var indicatorResult = await handler.Handle(IndicatorCommandBase.Create<GapIndicatorCommand>(stateSlot, "AAPL", template, candle), default);

    Assert.Equal(expected, indicatorResult.Percent);
    Assert.True(indicatorResult.IsTriggered);

    var secondIndicatorResult = await handler.Handle(IndicatorCommandBase.Create<GapIndicatorCommand>(stateSlot, "AAPL", template, candle), default);

    Assert.False(secondIndicatorResult.IsTriggered);
  }

  private class ClassData : TheoryData<double, DateTime, DateTime, double>
  {
    // Anchored to the tail of the fixture dataset: the candle cache materializes a rolling
    // GetMaxPeriod(Day)-long window ending at UtcNow, so early-2021 windows (the classic
    // post-hike 6% gap of April 2021) silently fall out of the readable window as the clock
    // advances - this pair stays reachable as long as the dataset itself is.
    public ClassData()
    {
      Add(1, new DateTime(2022, 4, 5), new DateTime(2022, 4, 6), 1.376356367789839);
    }
  }
}