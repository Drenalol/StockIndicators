using LazyCache;
using Microsoft.Extensions.Options;
using Moq;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.GetInstrumentLogo;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Instruments.Logos;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class GetInstrumentLogoQueryHandlerTests
{
  private static readonly InstrumentLogoData Fresh = new([1, 2, 3], "image/png", "logo.dev", DateTime.UtcNow);

  private sealed class StubSource(Func<InstrumentLogoRequest, CancellationToken, Task<InstrumentLogoData?>> resolve) : IInstrumentLogoSource
  {
    public int Calls;

    public Task<InstrumentLogoData?> TryResolveAsync(InstrumentLogoRequest request, CancellationToken cancellationToken)
    {
      Calls++;
      return resolve(request, cancellationToken);
    }
  }

  private static (GetInstrumentLogoQueryHandler Handler, IAppCache Cache, Mock<IInstrumentLogoStore> Store) CreateHandler(params IInstrumentLogoSource[] sources)
  {
    IAppCache cache = new FakeAppCache();

    var store = new Mock<IInstrumentLogoStore>();
    var cacheReader = new Mock<IInstrumentCacheReader>();

    var handler = new GetInstrumentLogoQueryHandler(
      cache,
      store.Object,
      cacheReader.Object,
      sources,
      Options.Create(new LogoOptions()),
      new Serilog.LoggerConfiguration().CreateLogger());

    return (handler, cache, store);
  }

  [Fact]
  public async Task Returns_logo_and_caches_it_when_sources_resolve()
  {
    var source = new StubSource((_, _) => Task.FromResult<InstrumentLogoData?>(Fresh));
    var (handler, cache, store) = CreateHandler(source);

    var result = await handler.Handle(new GetInstrumentLogoQuery("AAPL", null), default);

    Assert.Same(Fresh, result);
    Assert.Equal(1, source.Calls);

    store.Verify(s => s.SaveAsync(InstrumentLogoKeys.For("AAPL", null), Fresh, It.IsAny<CancellationToken>()), Times.Once);
    store.Verify(s => s.SaveNegativeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    // L1 now answers without re-resolving
    var second = await handler.Handle(new GetInstrumentLogoQuery("AAPL", null), default);

    Assert.Same(Fresh, second);
    Assert.Equal(1, source.Calls);
  }

  [Fact]
  public async Task Serves_L2_entry_without_touching_sources()
  {
    var store = new Mock<IInstrumentLogoStore>();
    IAppCache cache = new FakeAppCache();
    var source = new StubSource((_, _) => throw new InvalidOperationException("must not be called"));

    var handler = new GetInstrumentLogoQueryHandler(
      cache, store.Object, new Mock<IInstrumentCacheReader>().Object, [source],
      Options.Create(new LogoOptions()), new Serilog.LoggerConfiguration().CreateLogger());

    store
      .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(InstrumentLogoCacheEntry.From(Fresh));

    var result = await handler.Handle(new GetInstrumentLogoQuery("SBER", "MOEX"), default);

    Assert.Same(Fresh, result);
    Assert.Equal(0, source.Calls);
  }

  [Fact]
  public async Task Caches_negative_when_no_source_has_a_logo()
  {
    var miss = new StubSource((_, _) => Task.FromResult<InstrumentLogoData?>(null));
    var (handler, _, store) = CreateHandler(miss);

    var result = await handler.Handle(new GetInstrumentLogoQuery("ZZZZ", "MOEX"), default);

    Assert.Null(result);
    store.Verify(s => s.SaveNegativeAsync(InstrumentLogoKeys.For("ZZZZ", "MOEX"), It.IsAny<CancellationToken>()), Times.Once);

    // The negative marker is cached in L1: a second request does not re-resolve
    var second = await handler.Handle(new GetInstrumentLogoQuery("ZZZZ", "MOEX"), default);

    Assert.Null(second);
    Assert.Equal(1, miss.Calls);
  }

  [Fact]
  public async Task A_failing_source_does_not_break_the_cascade_order()
  {
    var first = new StubSource((_, _) => throw new HttpRequestException("boom"));
    var second = new StubSource((_, _) => Task.FromResult<InstrumentLogoData?>(Fresh));
    var third = new StubSource((_, _) => Task.FromResult<InstrumentLogoData?>(Fresh));

    var (handler, _, _) = CreateHandler(first, second, third);

    var result = await handler.Handle(new GetInstrumentLogoQuery("AAPL", null), default);

    Assert.Same(Fresh, result);
    Assert.Equal(1, first.Calls);
    Assert.Equal(1, second.Calls);
    Assert.Equal(0, third.Calls);
  }

  [Fact]
  public async Task Stale_logo_is_revalidated_in_the_background_and_L1_gets_the_fresh_bytes()
  {
    var stale = new InstrumentLogoData([9, 9, 9], "image/png", "tinkoff", DateTime.UtcNow.AddDays(-31));
    var store = new Mock<IInstrumentLogoStore>();
    var cacheReader = new Mock<IInstrumentCacheReader>();
    cacheReader.Setup(r => r.IsWarm).Returns(true);
    IAppCache cache = new FakeAppCache();

    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var refresher = new StubSource(
      async (_, _) =>
      {
        ready.TrySetResult();
        return new InstrumentLogoData([7, 7], "image/png", "tinkoff", DateTime.UtcNow);
      });

    store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(InstrumentLogoCacheEntry.From(stale));

    var handler = new GetInstrumentLogoQueryHandler(
      cache, store.Object, cacheReader.Object, [refresher],
      Options.Create(new LogoOptions { RevalidateAfterDays = 30 }), new Serilog.LoggerConfiguration().CreateLogger());

    var result = await handler.Handle(new GetInstrumentLogoQuery("SBER", "MOEX"), default);

    // The stale entry is served immediately (stale-while-revalidate)
    Assert.Same(stale, result);

    // The background revalidation re-resolves the sources and stores fresh bytes
    await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await WaitUntil(() => cache.TryGetValue<InstrumentLogoCacheEntry>(InstrumentLogoKeys.For("SBER", "MOEX"), out var entry) && !entry.Negative && entry.Logo!.FetchedAtUtc > stale.FetchedAtUtc);

    store.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<InstrumentLogoData>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
  }

  private static async Task WaitUntil(Func<bool> condition)
  {
    var deadline = DateTime.UtcNow.AddSeconds(5);

    while (!condition())
    {
      Assert.True(DateTime.UtcNow < deadline, "the background revalidation did not finish in time");
      await Task.Delay(25);
    }
  }
}
