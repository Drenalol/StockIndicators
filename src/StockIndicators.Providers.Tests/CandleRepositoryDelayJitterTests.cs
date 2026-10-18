using Moq;
using Serilog.Core;
using StockIndicators.Abstract;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Providers.Tinkoff;
using Xunit;

namespace StockIndicators.Providers.Tests;

/// <summary>
/// The provider's free-tier "15-minute delay" is approximate: minutes just past the history
/// horizon can still be unpublished while the response already carries their neighbours.
/// Minute/Hour empty markers are never re-probed (only Day/Week/Month markers are), so
/// stamping such a fresh gap turns a publication hiccup into a permanent hole - the fresh
/// gaps must stay unmarked until they settle.
/// </summary>
public class CandleRepositoryDelayJitterTests
{
  private static readonly DateTime Gap1 = DateTime.UtcNow.AddMinutes(-35);
  private static readonly DateTime Gap2 = DateTime.UtcNow.AddMinutes(-34);
  private static readonly DateTime BarSlot = DateTime.UtcNow.AddMinutes(-33);

  [Fact]
  public async Task GetCandles_FreshGaps_MissingFromANonEmptyResponse_StayUnmarked()
  {
    var cache = CreateCacheMock((Gap1, null), (Gap2, null), (BarSlot, Bar(BarSlot)));

    var saved = new List<Candle>();
    CaptureSaved(cache, saved);

    // The provider answers the request but only carries the newest minute: the two fresh
    // gaps are delay jitter, not missing trades
    var source = CreateSourceReturning([Bar(BarSlot)]);
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    var candles = await repository.GetCandlesAsync("AAPL", "NASDAQ", Gap1, BarSlot, TimeFrame.Minute, default);

    Assert.Single(candles);
    Assert.DoesNotContain(saved, candle => !candle.IsNotEmpty);
  }

  [Fact]
  public async Task GetCandles_SettledGaps_MissingFromANonEmptyResponse_AreStamped()
  {
    var oldGap1 = DateTime.UtcNow.AddHours(-4);
    var oldGap2 = oldGap1.AddMinutes(1);
    var oldBar = oldGap2.AddMinutes(1);

    var cache = CreateCacheMock((oldGap1, null), (oldGap2, null), (oldBar, Bar(oldBar)));

    var saved = new List<Candle>();
    CaptureSaved(cache, saved);

    var source = CreateSourceReturning([Bar(oldBar)]);
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    await repository.GetCandlesAsync("AAPL", "NASDAQ", oldGap1, oldBar, TimeFrame.Minute, default);

    // The gaps are hours old: the provider really has nothing for them, the markers
    // are final and keep further requests from re-probing
    Assert.Equal(2, saved.Count(candle => !candle.IsNotEmpty));
  }

  private static Candle Bar(DateTime slot) =>
    new("AAPL", slot, TimeFrame.Minute, 330m, 337m, 329m, 336m, 30_000m, exchangeName: "NASDAQ");

  private static void CaptureSaved(Mock<ICandleCache> cache, List<Candle> into) =>
    cache
      .Setup(c => c.AddRangeAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .Callback<IEnumerable<Candle>, bool, CancellationToken>((batch, _, _) => into.AddRange(batch))
      .Returns(Task.CompletedTask);

  private static Mock<ICandleCache> CreateCacheMock(params (DateTime Slot, Candle? Candle)[] slots)
  {
    var cache = new Mock<ICandleCache>();

    cache
      .Setup(m => m.GetLastAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(slots.Select(slot => new CandleResult(slot.Slot, slot.Candle)));

    cache
      .Setup(m => m.AddRangeAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    return cache;
  }

  private static Mock<IInstrumentSource> CreateSourceReturning(IEnumerable<Candle> candles)
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(m => m.GetInstrumentAsync("AAPL", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TinkoffInstrument("AAPL", "NASDAQ", null, "USD", "Apple", "US0378331005", "Tech", "Stock"));
    source
      .Setup(m => m.GetHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Returns((string _, DateTime _, DateTime _, TimeFrame _, CancellationToken _) => ToEnumerable(candles));
    return source;
  }

  private static async IAsyncEnumerable<Candle> ToEnumerable(IEnumerable<Candle> candles)
  {
    await Task.Yield();
    foreach (var candle in candles)
      yield return candle;
  }
}
