using Serilog.Core;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.RoundPrice;
using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Indicators.Tests.Common;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class RoundPriceIndicatorTests : IndicatorTestsBase
{
  public RoundPriceIndicatorTests(IndicatorCacheFixture indicatorCacheFixture, CandleRepositoryFixture candleRepositoryFixture) : base(indicatorCacheFixture, candleRepositoryFixture)
  {
  }

  [Theory]
  [ClassData(typeof(RoundPriceData))]
  public async Task RoundPriceTest(DateTime dateTime, decimal accuracy, bool result, TimeFrame timeFrame)
  {
    var handler = new RoundPriceIndicatorCommandHandler(Logger.None);

    var template = new RoundPriceIndicatorTemplate
    {
      Accuracy = accuracy
    };

    var candles = await GetAppleCandlesAsync(timeFrame, dateTime, dateTime);

    var indicatorResult = await handler.Handle(IndicatorCommandBase.Create<RoundPriceIndicatorCommand>(Guid.NewGuid(), "AAPL", template, candles), default);

    Assert.Equal(result, indicatorResult.IsTriggered);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task RoundPrice_EvaluatesTheLastCandle_NotTheOldestOfTheWindow(bool lastIsRound)
  {
    // The rules pipeline delivers the series ascending: the last candle is the one being
    // evaluated (and formed). The oldest candle of the window must not decide the result.
    var handler = new RoundPriceIndicatorCommandHandler(Logger.None);

    var template = new RoundPriceIndicatorTemplate { Accuracy = 100m };

    var oldest = new Candle("AAPL", new DateTime(2026, 10, 1), TimeFrame.Day, 500m, 500m, 500m, lastIsRound ? 150m : 157m, 10m);
    var last = new Candle("AAPL", new DateTime(2026, 10, 15), TimeFrame.Day, 500m, 500m, 500m, lastIsRound ? 1700m : 1733m, 10m);

    var indicatorResult = await handler.Handle(
      IndicatorCommandBase.Create<RoundPriceIndicatorCommand>(Guid.NewGuid(), "AAPL", template, [oldest, last]),
      default);

    Assert.Equal(lastIsRound, indicatorResult.IsTriggered);
  }
}

public class RoundPriceData : TheoryData<DateTime, decimal, bool, TimeFrame>
{
  public RoundPriceData()
  {
    Add(new DateTime(2022, 2, 7), 2, true, TimeFrame.Day);
    Add(new DateTime(2022, 2, 7), 4, true, TimeFrame.Day);
    Add(new DateTime(2022, 2, 7), 43, true, TimeFrame.Day);
    Add(new DateTime(2022, 2, 7), 172, true, TimeFrame.Day);
    Add(new DateTime(2022, 2, 7), 173, false, TimeFrame.Day);
    Add(new DateTime(2022, 2, 7), 173, false, TimeFrame.Day);
    // 2022-04 is the newest month in the fixture and within the Month cache window (250 months)
    Add(new DateTime(2022, 4, 1), 501, false, TimeFrame.Month);
  }
}