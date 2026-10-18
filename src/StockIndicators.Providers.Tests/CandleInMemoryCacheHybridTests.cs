using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Data;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class CandleInMemoryCacheHybridTests
{
  private const string Key = "btcusdt:binance:minute:candles";

  private readonly CandleInMemoryCache _cache;
  private readonly FakeCandleStore _store;

  public CandleInMemoryCacheHybridTests()
  {
    _store = new FakeCandleStore();
    _cache = new CandleInMemoryCache(new FakeAppCache(), _store, Logger.None);
  }

  [Fact]
  public async Task GetLast_HydratesL1_FromStore_WhenL1IsMiss()
  {
    // Raw dates carry seconds — hydration must normalize them to the minute
    _store.Seed(Key,
      NewCandle(5, close: 100m, secondsOffset: 30),
      NewCandle(4, close: 101m, secondsOffset: 45));

    var results = (await _cache.GetLastAsync("BTCUSDT", "BINANCE", TimeFrame.Minute)).Where(r => r.Candle != null).ToArray();

    Assert.Equal(2, results.Length);
    var byDate = ToCandles(results);
    Assert.Equal(100m, byDate[AtMinute(5)].Close);
    Assert.Equal(101m, byDate[AtMinute(4)].Close);
    Assert.Equal(1, _store.LoadCalls);

    // Second read is served from L1 — no second remote hit
    await _cache.GetLastAsync("BTCUSDT", "BINANCE", TimeFrame.Minute);
    Assert.Equal(1, _store.LoadCalls);
  }

  [Fact]
  public async Task AddRange_MergesStoredHistory_WithIncomingBatch_WhenL1IsMiss()
  {
    _store.Seed(Key, NewCandle(10, close: 90m), NewCandle(9, close: 91m));

    Candle[] batch = [NewCandle(2, close: 95m, secondsOffset: 20), NewCandle(1, close: 96m, secondsOffset: 40)];

    await _cache.AddRangeAsync(batch, isRealTime: false);

    var results = (await _cache.GetLastAsync("BTCUSDT", "BINANCE", TimeFrame.Minute)).Where(r => r.Candle != null).ToArray();

    var byDate = ToCandles(results);
    Assert.Equal(4, results.Length);
    Assert.Equal(90m, byDate[AtMinute(10)].Close);
    Assert.Equal(91m, byDate[AtMinute(9)].Close);
    Assert.Equal(95m, byDate[AtMinute(2)].Close);
    Assert.Equal(96m, byDate[AtMinute(1)].Close);
    Assert.Equal(1, _store.LoadCalls);

    // Warm L1: the next write merges into it without another remote load
    await _cache.AddRangeAsync([NewCandle(1, close: 97m)], isRealTime: false);
    Assert.Equal(1, _store.LoadCalls);

    var updated = (await _cache.GetLastAsync("BTCUSDT", "BINANCE", TimeFrame.Minute)).Where(r => r.Candle != null).ToArray();
    Assert.Equal(97m, ToCandles(updated)[AtMinute(1)].Close);
  }

  [Fact]
  public async Task AddRange_PersistsBatchToStore_WithCacheTtl()
  {
    Candle[] batch = [NewCandle(3, close: 10m), NewCandle(2, close: 11m), NewCandle(1, close: 12m)];

    await _cache.AddRangeAsync(batch, isRealTime: false);

    var saved = _store.SavedBatches[Key];
    Assert.Equal(3, saved.Count);
    Assert.Equal(
      batch.Select(c => (c.Ticker, c.ExchangeName, c.TimeFrame, c.Open, c.High, c.Low, c.Close, c.Volume, c.IsNotEmpty, c.IsCompleted))
        .OrderBy(c => c.Close),
      saved.Select(c => (c.Ticker, c.ExchangeName, c.TimeFrame, c.Open, c.High, c.Low, c.Close, c.Volume, c.IsNotEmpty, c.IsCompleted))
        .OrderBy(c => c.Close)
    );
    Assert.Equal(TimeSpan.FromDays(5), _store.SavedTtls[Key]);
  }

  [Fact]
  public async Task AddRange_SwallowsStoreSaveFailure_KeepsCandlesInL1()
  {
    _store.SaveShouldThrow = true;
    Candle[] batch = [NewCandle(2, close: 10m), NewCandle(1, close: 11m)];

    await _cache.AddRangeAsync(batch, isRealTime: false);

    var results = (await _cache.GetLastAsync("BTCUSDT", "BINANCE", TimeFrame.Minute)).Where(r => r.Candle != null).ToArray();
    Assert.Equal(2, results.Length);
    Assert.Equal(11m, ToCandles(results)[AtMinute(1)].Close);
  }

  [Fact]
  public async Task GetLast_SwallowsStoreLoadFailure_ReturnsL1Data()
  {
    _store.Seed(Key, NewCandle(9, close: 91m));
    _store.LoadShouldThrow = true;

    await _cache.AddRangeAsync([NewCandle(1, close: 96m)], isRealTime: false);

    var results = (await _cache.GetLastAsync("BTCUSDT", "BINANCE", TimeFrame.Minute)).Where(r => r.Candle != null).ToArray();
    Assert.Single(results);
    Assert.Equal(96m, results[0].Candle!.Close);
  }

  [Fact]
  public void Candle_SurvivesJsonRoundTrip_WithDiSerializerSettings()
  {
    var serializer = new DefaultSerializer(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }, Logger.None);
    var candle = NewCandle(1, close: 1234.5678m);

    var json = serializer.Serialize(candle);
    var restored = serializer.Deserialize<Candle>(json);

    Assert.Equal(candle.Ticker, restored.Ticker);
    Assert.Equal(candle.ExchangeName, restored.ExchangeName);
    Assert.Equal(candle.Timestamp, restored.Timestamp);
    Assert.Equal(candle.Timestamp.Kind, restored.Timestamp.Kind);
    Assert.Equal(candle.TimeFrame, restored.TimeFrame);
    Assert.Equal(candle.Open, restored.Open);
    Assert.Equal(candle.High, restored.High);
    Assert.Equal(candle.Low, restored.Low);
    Assert.Equal(candle.Close, restored.Close);
    Assert.Equal(candle.Volume, restored.Volume);
    Assert.Equal(candle.IsNotEmpty, restored.IsNotEmpty);
    Assert.Equal(candle.IsCompleted, restored.IsCompleted);
    Assert.Equal(candle.Updated, restored.Updated);
  }

  private static Candle NewCandle(int minutesAgo, decimal close, int secondsOffset = 0) =>
    new("BTCUSDT", AtMinute(minutesAgo).AddSeconds(secondsOffset), TimeFrame.Minute, 1m, 2m, 0.5m, close, 1000, exchangeName: "BINANCE");

  private static DateTime AtMinute(int minutesAgo)
  {
    var dt = DateTime.UtcNow.AddMinutes(-minutesAgo);
    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc);
  }

  private static Dictionary<DateTime, Candle> ToCandles(CandleResult[] results) =>
    results.ToDictionary(r => r.DateTime, r => r.Candle!);

  private sealed class FakeCandleStore : ICandleStore
  {
    private readonly Dictionary<string, Dictionary<DateTime, Candle>> _store = new();

    public int LoadCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public Dictionary<string, List<Candle>> SavedBatches { get; } = new();

    public Dictionary<string, TimeSpan> SavedTtls { get; } = new();

    public bool LoadShouldThrow { get; set; }

    public bool SaveShouldThrow { get; set; }

    public void Seed(string key, params Candle[] candles) => _store[key] = candles.ToDictionary(c => c.Timestamp);

    public Task<Dictionary<DateTime, Candle>?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
      LoadCalls++;

      if (LoadShouldThrow)
        throw new InvalidOperationException("redis is down");

      return Task.FromResult(_store.TryGetValue(key, out var candles) ? candles : null);
    }

    public Task SaveCandlesAsync(string key, IEnumerable<Candle> candles, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
      SaveCalls++;

      if (SaveShouldThrow)
        throw new InvalidOperationException("redis is down");

      SavedBatches[key] = candles.ToList();
      SavedTtls[key] = ttl;

      return Task.CompletedTask;
    }
  }
}
