using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Indicators.Tests.Common;
using Xunit;

namespace StockIndicators.Indicators.Tests;

[Collection(nameof(SharedContext))]
public class IndicatorTestsBase
{
  protected readonly IIndicatorCache IndicatorCache;
  private readonly ICandleRepository _candleRepository;

  protected IndicatorTestsBase(IndicatorCacheFixture indicatorCacheFixture, CandleRepositoryFixture candleRepositoryFixture)
  {
    IndicatorCache = indicatorCacheFixture.IndicatorCache;
    _candleRepository = candleRepositoryFixture.CandleRepository;
  }

  protected Task<Candle[]> GetAppleCandlesAsync(TimeFrame timeFrame, DateTime start, DateTime end)
  {
    start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
    end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

    return _candleRepository.GetCandlesAsync("BBG000B9XRY4", null, start, end, timeFrame);
  }
}