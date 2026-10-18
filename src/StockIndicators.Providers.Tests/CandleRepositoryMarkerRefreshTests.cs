using Moq;
using Serilog;
using Serilog.Core;
using StockIndicators.Abstract;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Providers.Tinkoff;
using Xunit;

namespace StockIndicators.Providers.Tests;

/// <summary>
/// Day-precision empty markers must stay re-checkable: providers materialize a day's
/// aggregate only around/after that day's session, so a slot probed too early (the first
/// chart request on a morning) comes back empty and must not become a permanent hole.
/// </summary>
public class CandleRepositoryMarkerRefreshTests
{
  private static readonly DateTime NowUtc = DateTime.UtcNow;
  private static readonly DateTime TodaySlot = NowUtc.Date;
  private static readonly DateTime YesterdaySlot = NowUtc.Date.AddDays(-1);

  [Fact]
  public async Task GetLastCandles_RequeriesStaleDayMarker_WhenProviderNowHasTheBar()
  {
    var cache = CreateCacheMock(
      (YesterdaySlot, Marker(YesterdaySlot, hoursAgo: 2)),
      (TodaySlot, Marker(TodaySlot, hoursAgo: 1)));

    var source = CreateSourceReturningBarForYesterday(336.67m);
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    var candles = await repository.GetLastCandlesAsync("AAPL", "NASDAQ", 130, TimeFrame.Day, default);

    var healed = Assert.Single(candles);
    Assert.Equal(336.67m, healed.Close);
    cache.Verify(
      c => c.AddRangeAsync(It.IsAny<IEnumerable<Candle>>(), false, It.IsAny<CancellationToken>()),
      Times.AtLeastOnce);
  }

  [Fact]
  public async Task GetLastCandles_DoesNotProbe_SoleNullSlotOfCurrentDay()
  {
    // The newest candle of the current date with no marker: the live pipeline delivers its
    // forming candle, so an empty provider probe per chart request buys nothing
    var cache = CreateCacheMock((TodaySlot, null));

    var source = CreateSourceReturningNothing();
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    var candles = await repository.GetLastCandlesAsync("AAPL", "NASDAQ", 130, TimeFrame.Day, default);

    Assert.Empty(candles);
    source.Verify(
      s => s.GetHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetLastCandles_TrustsMarkers_OlderThanTheRefreshWindow()
  {
    // A holiday marker from six days ago is final: no provider probe per request
    var holidaySlot = NowUtc.Date.AddDays(-6);
    var cache = CreateCacheMock((holidaySlot, Marker(holidaySlot, hoursAgo: 2)));

    var source = CreateSourceReturningNothing();
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    var candles = await repository.GetLastCandlesAsync("AAPL", "NASDAQ", 130, TimeFrame.Day, default);

    Assert.Empty(candles);
    source.Verify(
      s => s.GetHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetLastCandles_DoesNotRethrottle_FreshlyProbedMarker()
  {
    // The slot was probed minutes ago (marker re-stamped): the 15-minute spacing holds
    var cache = CreateCacheMock((YesterdaySlot, Marker(YesterdaySlot, minutesAgo: 5)));

    var source = CreateSourceReturningNothing();
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    var candles = await repository.GetLastCandlesAsync("AAPL", "NASDAQ", 130, TimeFrame.Day, default);

    Assert.Empty(candles);
    source.Verify(
      s => s.GetHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetLastCandles_RecheckStillEmpty_RemarksMarker_AndSavesIt()
  {
    var cache = CreateCacheMock(
      (YesterdaySlot, Marker(YesterdaySlot, hoursAgo: 2)),
      (TodaySlot, Marker(TodaySlot, hoursAgo: 1)));

    var saved = new List<IEnumerable<Candle>>();
    cache
      .Setup(c => c.AddRangeAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .Callback<IEnumerable<Candle>, bool, CancellationToken>((batch, _, _) => saved.Add(batch.ToList()))
      .Returns(Task.CompletedTask);

    var source = CreateSourceReturningNothing();
    var repository = new CachedCandleRepository(cache.Object, [source.Object], Logger.None);

    var candles = await repository.GetLastCandlesAsync("AAPL", "NASDAQ", 130, TimeFrame.Day, default);

    Assert.Empty(candles);
    // Both stale markers were re-probed, found empty again and re-stamped (that is what
    // throttles the next probe)
    Assert.Equal(2, saved.SelectMany(batch => batch).Count(s => !s.IsNotEmpty));
  }

  private static Candle Marker(DateTime slot, double? hoursAgo = null, double? minutesAgo = null)
  {
    var marker = Candle.Empty("AAPL", slot, TimeFrame.Day, "NASDAQ");

    marker.Updated = hoursAgo is not null
      ? DateTime.UtcNow.AddHours(-hoursAgo.Value)
      : DateTime.UtcNow.AddMinutes(-minutesAgo!.Value);

    return marker;
  }

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

  private static Mock<IInstrumentSource> CreateSourceReturningBarForYesterday(decimal close)
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(m => m.GetInstrumentAsync("AAPL", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TinkoffInstrument("AAPL", "NASDAQ", null, "USD", "Apple", "US0378331005", "Tech", "Stock"));
    source
      .Setup(m => m.GetHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Returns((string _, DateTime _, DateTime _, TimeFrame _, CancellationToken _) =>
        ToEnumerable([new Candle("AAPL", DateTime.SpecifyKind(YesterdaySlot.AddHours(4), DateTimeKind.Utc), TimeFrame.Day, 330m, 337m, 329m, close, 30_000_000m, exchangeName: "NASDAQ")]));
    return source;
  }

  private static Mock<IInstrumentSource> CreateSourceReturningNothing()
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(m => m.GetInstrumentAsync("AAPL", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TinkoffInstrument("AAPL", "NASDAQ", null, "USD", "Apple", "US0378331005", "Tech", "Stock"));
    source
      .Setup(m => m.GetHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Returns((string _, DateTime _, DateTime _, TimeFrame _, CancellationToken _) => ToEnumerable(Array.Empty<Candle>()));
    return source;
  }

  private static async IAsyncEnumerable<Candle> ToEnumerable(IEnumerable<Candle> candles)
  {
    await Task.Yield();
    foreach (var candle in candles)
      yield return candle;
  }
}
