using Moq;
using Serilog;
using Serilog.Core;
using StockIndicators.Abstract;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Providers.Tinkoff;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class CandleRepositoryScopingTests
{
  private static readonly DateTime T0 = new(2026, 8, 16, 13, 30, 0);
  private static readonly DateTime T1 = new(2026, 8, 16, 13, 31, 0);
  private static readonly DateTime T2 = new(2026, 8, 16, 13, 32, 0);

  [Fact]
  public async Task GetCandles_SkipsSource_WhenExchangeDoesNotMatch()
  {
    var wrongSource = CreateSourceMock("SBER", "NASDAQ", fillHole: true, holeVolume: 111111m);
    var correctSource = CreateSourceMock("SBER", "MOEX", fillHole: true, holeVolume: 777777m);
    var repository = CreateRepository(CreateCacheMock(), wrongSource.Object, correctSource.Object);

    var candles = await repository.GetCandlesAsync("SBER", "MOEX", T0, T2, TimeFrame.Minute, default);

    var holeCandle = candles.Single(c => c.Timestamp == T1);
    Assert.Equal(777777m, holeCandle.Volume);
    wrongSource.Verify(
      s => s.GetHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetCandles_AllowsAnySource_WhenExchangeIsNull()
  {
    var source = CreateSourceMock("SBER", "MOEX", fillHole: true, holeVolume: 555555m);
    var repository = CreateRepository(CreateCacheMock(), source.Object);

    var candles = await repository.GetCandlesAsync("SBER", null, T0, T2, TimeFrame.Minute, default);

    var holeCandle = candles.Single(c => c.Timestamp == T1);
    Assert.Equal(555555m, holeCandle.Volume);
    source.Verify(
      s => s.GetHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetCandles_SkipsSource_WhenInstrumentNotFound()
  {
    var brokenSource = new Mock<IInstrumentSource>();
    brokenSource
      .Setup(m => m.GetInstrumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new KeyNotFoundException("instrument not found"));

    var fallbackSource = CreateSourceMock("SBER", "MOEX", fillHole: true, holeVolume: 333333m);
    var repository = CreateRepository(CreateCacheMock(), brokenSource.Object, fallbackSource.Object);

    var candles = await repository.GetCandlesAsync("SBER", "MOEX", T0, T2, TimeFrame.Minute, default);

    var holeCandle = candles.Single(c => c.Timestamp == T1);
    Assert.Equal(333333m, holeCandle.Volume);
  }

  private static CachedCandleRepository CreateRepository(Mock<ICandleCache> cache, params IInstrumentSource[] sources) =>
    new(cache.Object, sources, Logger.None);

  private static Mock<ICandleCache> CreateCacheMock()
  {
    var cache = new Mock<ICandleCache>();
    cache
      .Setup(m => m.GetLastAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new[]
      {
        new CandleResult(T0, new Candle("SBER", T0, TimeFrame.Minute, 10m, 10m, 10m, 10m, 100)),
        new CandleResult(T1, null),
        new CandleResult(T2, null)
      });
    return cache;
  }

  private static Mock<IInstrumentSource> CreateSourceMock(string ticker, string exchange, bool fillHole, decimal holeVolume)
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(m => m.GetInstrumentAsync(ticker, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TinkoffInstrument(ticker, exchange, null, "RUB", "Name", "IN000", "Sector", "Stock"));
    source
      .Setup(m => m.GetHistoryAsync(ticker, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Returns((string _, DateTime _, DateTime _, TimeFrame _, CancellationToken _) => fillHole ? HoleCandles(ticker, holeVolume) : EmptyCandles());
    return source;
  }

  private static async IAsyncEnumerable<Candle> HoleCandles(string ticker, decimal volume)
  {
    await Task.Yield();
    yield return new Candle(ticker, T1, TimeFrame.Minute, 10m, 10m, 10m, 10m, volume);
    yield return new Candle(ticker, T2, TimeFrame.Minute, 10m, 10m, 10m, 10m, 42);
  }

  private static async IAsyncEnumerable<Candle> EmptyCandles()
  {
    await Task.Yield();
    yield break;
  }
}
