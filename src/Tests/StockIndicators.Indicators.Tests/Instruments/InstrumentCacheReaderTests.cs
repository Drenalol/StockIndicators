using LazyCache;
using LazyCache.Providers;
using StockIndicators.Abstract;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Domain;
using StockIndicators.Infrastructure.Domain.Instruments;
using Xunit;

namespace StockIndicators.Indicators.Tests.Instruments;

public class InstrumentsCacheReaderTests : IDisposable
{
  public InstrumentsCacheReaderTests() => ClearGlobal();

  private static void ClearGlobal() => new CachingService().Remove(InstrumentsCache.AllKey);

  public void Dispose() => ClearGlobal();

  private record Instrument(string Ticker, string? ExchangeName, string Source) : IInstrument
  {
    public string? ClassCode => null;
    public string? Figi => null;
    public string Currency => "usdt";
    public string? Name => null;
    public string? Isin => null;
    public string? Sector => null;
    public string? Type => "Crypto";
  }

  private InstrumentsCacheReader CreateReader(params InstrumentDto[] instruments)
  {
    var cache = new CachingService();
    cache.Add(InstrumentsCache.AllKey, instruments.ToList(), DateTimeOffset.UtcNow.AddMinutes(5));
    return new InstrumentsCacheReader(cache);
  }

  [Fact]
  public async Task TryGet_MatchingSourceAndTicker_ReturnsEntry()
  {
    var reader = CreateReader(new InstrumentDto { Ticker = "BTCUSDT", ExchangeName = "BINANCE", Source = "binance" });

    var match = await reader.TryGetAsync("BTCUSDT", "binance");

    Assert.NotNull(match);
    Assert.Equal("BINANCE", match.ExchangeName);
  }

  [Fact]
  public async Task TryGet_DifferentSource_ReturnsNull()
  {
    var reader = CreateReader(new InstrumentDto { Ticker = "BTCUSDT", ExchangeName = "BINANCE", Source = "binance" });

    Assert.Null(await reader.TryGetAsync("BTCUSDT", "alpaca"));
  }

  [Fact]
  public async Task TryGet_ColdCache_ReturnsNull()
  {
    // No CreateReader call: the global LazyCache static has been cleared in the constructor.
    var reader = new InstrumentsCacheReader(new CachingService());

    Assert.Null(await reader.TryGetAsync("BTCUSDT", "binance"));
  }

  [Fact]
  public async Task TryGet_WithoutSource_MatchesAnySource()
  {
    var reader = CreateReader(new InstrumentDto { Ticker = "BTCUSDT", ExchangeName = "BINANCE", Source = "binance" });

    Assert.NotNull(await reader.TryGetAsync("BTCUSDT", null));
  }
}
