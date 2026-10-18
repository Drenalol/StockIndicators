using LazyCache;
using Microsoft.Extensions.Options;
using Moq;
using Serilog.Core;
using StockIndicators.Abstract;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Providers;
using StockIndicators.Infrastructure.Domain.Instruments;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class InstrumentsWarmupServiceTests
{
  private sealed record TestInstrument(string Ticker, string ExchangeName) : IInstrument
  {
    public string? ClassCode => null;
    public string? Figi => null;
    public string Currency => "USD";
    public string? Name => null;
    public string? Isin => null;
    public string? Sector => null;
    public string Type => "Stock";
    public string Source => "test";
  }

  private static IInstrument Instrument(string ticker, string exchange) => new TestInstrument(ticker, exchange);

  private static Mock<IInstrumentSource> CreateSource(params (string Ticker, string Exchange)[] instruments)
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(s => s.GetInstrumentsAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(instruments.Select(i => (IInstrument)Instrument(i.Ticker, i.Exchange)).ToHashSet());
    return source;
  }

  private static Mock<IInstrumentSource> CreateFailingSource()
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(s => s.GetInstrumentsAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("source is down"));
    return source;
  }

  /// <summary>Source that answers after 2s, or cancels with the passed token.</summary>
  private static Mock<IInstrumentSource> CreateSlowSource()
  {
    var source = new Mock<IInstrumentSource>();
    source
      .Setup(s => s.GetInstrumentsAsync(It.IsAny<CancellationToken>()))
      .Returns((CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(2), ct)
          .ContinueWith(_ => new HashSet<IInstrument> { Instrument("SBER", "MOEX") }));
    return source;
  }

  /// <summary>Records the table syncs the warm-up performs instead of touching Postgres.</summary>
  private sealed class RecordingTableSync : IInstrumentsTableSync
  {
    public List<InstrumentsLoadResult> Syncs { get; } = [];

    public Task SyncAsync(InstrumentsLoadResult result, CancellationToken cancellationToken)
    {
      Syncs.Add(result);
      return Task.CompletedTask;
    }
  }

  private static InstrumentsWarmupService CreateService(
    IAppCache cache,
    InstrumentsWarmupOptions options,
    params Mock<IInstrumentSource>[] sources) =>
    new(
      Options.Create(options),
      sources.Select(s => (IInstrumentSource)s.Object),
      cache,
      new RecordingTableSync(),
      new UnwiredDataProviders([]),
      Logger.None);

  private static List<InstrumentDto> CachedInstruments(IAppCache cache) =>
    cache.Get<List<InstrumentDto>>(InstrumentsCache.AllKey) ?? [];

  [Fact]
  public async Task WarmupAsync_CachesMergedInstruments_UnderSharedKey()
  {
    var cache = new FakeAppCache();
    var service = CreateService(
      cache,
      new InstrumentsWarmupOptions(),
      CreateSource(("SBER", "MOEX"), ("GASP", "MOEX"), ("AAPL", "NASDAQ")),
      CreateSource(("AAPL", "NASDAQ"), ("TSLA", "NASDAQ")));

    await service.WarmupAsync(CancellationToken.None);

    var instruments = CachedInstruments(cache);

    // AAPL appears in both sources and must be deduped (Ticker + ExchangeName)
    Assert.Equal(4, instruments.Count);
    Assert.Contains(instruments, i => i.Ticker == "SBER" && i.ExchangeName == "MOEX");
    Assert.Contains(instruments, i => i.Ticker == "GASP" && i.ExchangeName == "MOEX");
    Assert.Contains(instruments, i => i.Ticker == "AAPL" && i.ExchangeName == "NASDAQ");
    Assert.Contains(instruments, i => i.Ticker == "TSLA" && i.ExchangeName == "NASDAQ");
  }

  [Fact]
  public async Task WarmupAsync_IgnoresFailingSource_AndCachesRest()
  {
    var cache = new FakeAppCache();
    var service = CreateService(
      cache,
      new InstrumentsWarmupOptions(),
      CreateFailingSource(),
      CreateSource(("AAPL", "NASDAQ"), ("TSLA", "NASDAQ")));

    await service.WarmupAsync(CancellationToken.None);

    var instruments = CachedInstruments(cache);

    Assert.Equal(2, instruments.Count);
    Assert.Contains(instruments, i => i.Ticker == "AAPL" && i.ExchangeName == "NASDAQ");
    Assert.Contains(instruments, i => i.Ticker == "TSLA" && i.ExchangeName == "NASDAQ");
  }

  [Fact]
  public async Task WarmupAsync_OverwritesPreviousEntry()
  {
    var cache = new FakeAppCache();
    var first = CreateSource(("SBER", "MOEX"));
    var second = CreateSource(("AAPL", "NASDAQ"));

    var service = CreateService(cache, new InstrumentsWarmupOptions(), first);
    await service.WarmupAsync(CancellationToken.None);
    Assert.Single(CachedInstruments(cache));

    var swapped = CreateService(cache, new InstrumentsWarmupOptions(), second);
    await swapped.WarmupAsync(CancellationToken.None);

    var instruments = CachedInstruments(cache);

    Assert.Single(instruments);
    Assert.Equal("AAPL", instruments[0].Ticker);
  }

  [Fact]
  public async Task WarmupAsync_DiscardsResult_WhenCancelledMidLoad()
  {
    var cache = new FakeAppCache();
    var slowSource = CreateSlowSource();
    var service = CreateService(cache, new InstrumentsWarmupOptions(), slowSource);
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

    await Assert.ThrowsAsync<OperationCanceledException>(
      () => service.WarmupAsync(cts.Token));

    Assert.Empty(CachedInstruments(cache));
  }

  [Fact]
  public async Task StartAsync_WarmsCache_BeforeReturning()
  {
    var cache = new FakeAppCache();
    var service = CreateService(
      cache,
      new InstrumentsWarmupOptions(),
      CreateSource(("SBER", "MOEX"), ("AAPL", "NASDAQ")));

    try
    {
      await service.StartAsync(CancellationToken.None);
      Assert.Equal(2, CachedInstruments(cache).Count);

      await service.StopAsync(CancellationToken.None);
    }
    finally
    {
      await service.StopAsync(CancellationToken.None);
    }
  }

  [Fact]
  public async Task WarmupAsync_SyncsTable_WithLoadedSourcesOnly()
  {
    var cache = new FakeAppCache();
    var tableSync = new RecordingTableSync();

    var service = new InstrumentsWarmupService(
      Options.Create(new InstrumentsWarmupOptions()),
      [CreateSource(("SBER", "MOEX")).Object, CreateFailingSource().Object, CreateSource(("AAPL", "NASDAQ")).Object],
      cache,
      tableSync,
      new UnwiredDataProviders([]),
      Logger.None);

    await service.WarmupAsync(CancellationToken.None);

    var sync = Assert.Single(tableSync.Syncs);

    // Only the sources that answered make it into the prune set
    Assert.Equal(2, sync.Instruments.Count);
    Assert.Equal(["test"], sync.LoadedSources);
    Assert.Empty(sync.UnwiredSources);
  }

  [Fact]
  public async Task WarmupAsync_SyncsTable_PrunesUnwiredDataProviders()
  {
    var cache = new FakeAppCache();
    var tableSync = new RecordingTableSync();

    var service = new InstrumentsWarmupService(
      Options.Create(new InstrumentsWarmupOptions()),
      [CreateSource(("AAPL", "NASDAQ")).Object],
      cache,
      tableSync,
      new UnwiredDataProviders(["tinkoff"]),
      Logger.None);

    await service.WarmupAsync(CancellationToken.None);

    var sync = Assert.Single(tableSync.Syncs);

    // The unwired provider never answered, yet its catalog rows must still be pruned
    Assert.Equal(["test"], sync.LoadedSources);
    Assert.Equal(["tinkoff"], sync.UnwiredSources);
  }

  [Fact]
  public async Task StartAsync_KeepsStarting_WhenInitialWarmupTimesOut()
  {
    var cache = new FakeAppCache();
    var options = new InstrumentsWarmupOptions
    {
      InitialWarmupTimeout = TimeSpan.FromMilliseconds(200),
      FailureRetryInterval = TimeSpan.FromMinutes(1) // keep the retry loop out of this test
    };

    var service = CreateService(cache, options, CreateSlowSource());

    try
    {
      await service.StartAsync(CancellationToken.None);

      // The timed-out (partial) result must not be cached
      Assert.Empty(CachedInstruments(cache));

      await service.StopAsync(CancellationToken.None);
    }
    finally
    {
      await service.StopAsync(CancellationToken.None);
    }
  }
}
