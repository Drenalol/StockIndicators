using Serilog;
using Serilog.Core;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;

namespace StockIndicators.Providers.Tests;

/// <summary>
/// The realtime minute fold may only update the forming candle of its own period. When the
/// current period's candle is absent (the providers ship day/hour/week/month aggregates only
/// after the period closes), folding into the newest older candle would silently rewrite
/// completed history: yesterday's daily close/volume onto today's live values.
/// </summary>
public class CandleInMemoryCacheFoldTests
{
  private const string DayKey = "aapl:nasdaq:day:candles";
  private const string HourKey = "aapl:nasdaq:hour:candles";

  private readonly CandleInMemoryCache _cache = new(new FakeAppCache(), new RecordingStore(), Logger.None);

  [Fact]
  public async Task RealtimeMinute_DoesNotFoldIntoCompletedDayBar_WhenTodaysBarIsMissing()
  {
    var yesterday = DateTime.UtcNow.Date.AddDays(-1);
    var dayBar = NewDayBar(yesterday, close: 330m, volume: 30_000_000m);

    await _cache.AddRangeAsync([dayBar], isRealTime: false);

    // A live minute of today (no forming candle in the cache, only the old completed bar)
    var liveMinute = NewMinute(DateTime.UtcNow, close: 350m, volume: 5_000m);

    await _cache.AddRangeAsync([liveMinute], isRealTime: true);

    var stored = (await _cache.GetLastAsync("AAPL", "NASDAQ", TimeFrame.Day)).Where(r => r.Candle != null).ToArray();

    var unchanged = stored.Single(r => r.DateTime.Date == yesterday);
    Assert.Equal(330m, unchanged.Candle!.Close);
    Assert.Equal(30_000_000m, unchanged.Candle.Volume);
  }

  [Fact]
  public async Task RealtimeMinute_FoldsIntoTheFormingBarOfItsOwnDay()
  {
    var today = DateTime.UtcNow.Date;
    var forming = NewDayBar(today, close: 331m, volume: 1_000m);

    await _cache.AddRangeAsync([forming], isRealTime: false);

    var liveMinute = NewMinute(DateTime.UtcNow, close: 335m, volume: 400m);

    await _cache.AddRangeAsync([liveMinute], isRealTime: true);

    var stored = (await _cache.GetLastAsync("AAPL", "NASDAQ", TimeFrame.Day)).Where(r => r.Candle != null).ToArray();

    var updated = stored.Single(r => r.DateTime.Date == today);
    Assert.Equal(335m, updated.Candle!.Close);
    Assert.Equal(1_400m, updated.Candle.Volume);
  }

  [Fact]
  public async Task RealtimeMinute_DoesNotFoldIntoThePreviousHourBar()
  {
    var nowMinute = new DateTime(
      DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day,
      DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
    var previousHour = nowMinute.Date.AddHours(nowMinute.Hour - 1);

    // The previous hour's completed bar, stale enough for the fold's "not updated in 50s" gate
    var hourBar = new Candle("AAPL", previousHour, TimeFrame.Hour, 10m, 341m, 9m, 320m, 9_000_000m, exchangeName: "NASDAQ")
    {
      Updated = DateTime.UtcNow.AddSeconds(-120)
    };

    await _cache.AddRangeAsync([hourBar], isRealTime: false);

    await _cache.AddRangeAsync([NewMinute(nowMinute, close: 340m, volume: 300m)], isRealTime: true);

    var stored = (await _cache.GetLastAsync("AAPL", "NASDAQ", TimeFrame.Hour)).Where(r => r.Candle != null).ToArray();

    var unchanged = stored.Single(r => r.DateTime == previousHour);
    Assert.Equal(320m, unchanged.Candle!.Close);
    Assert.Equal(9_000_000m, unchanged.Candle.Volume);
  }

  private static Candle NewDayBar(DateTime utcDate, decimal close, decimal volume) =>
    new("AAPL", DateTime.SpecifyKind(utcDate, DateTimeKind.Utc), TimeFrame.Day, 10m, 340m, 9m, close, volume, exchangeName: "NASDAQ")
    {
      Updated = DateTime.UtcNow.AddSeconds(-120)
    };

  private static Candle NewMinute(DateTime utc, decimal close, decimal volume) =>
    new("AAPL", DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeFrame.Minute, 1m, 2m, 0.5m, close, volume, exchangeName: "NASDAQ");

  private sealed class RecordingStore : ICandleStore
  {
    public Task<Dictionary<DateTime, Candle>?> LoadAsync(string key, CancellationToken cancellationToken = default) =>
      Task.FromResult<Dictionary<DateTime, Candle>?>(null);

    public Task SaveCandlesAsync(string key, IEnumerable<Candle> candles, TimeSpan ttl, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }
}
