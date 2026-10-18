using System.Collections.Generic;
using System.Net.Http;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Serilog.Core;
using StockIndicators.Enrichers.Yahoo;
using StockIndicators.Enrichers.Yahoo.Api;
using StockIndicators.Enrichers.Yahoo.Internal;
using StockIndicators.Enrichers.Yahoo.Schema;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class YahooInstrumentEnricherTests
{
  private readonly Mock<IYahooEnrichmentApi> _apiMock = new();
  private readonly IAppCache _cache = new FakeAppCache();

  [Fact]
  public async Task BlankTicker_ReturnsNull_WithoutApiCall()
  {
    var enricher = CreateEnricher();

    var result = await enricher.GetEnrichmentAsync("  ");

    Assert.Null(result);
    _apiMock.Verify(m => m.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SearchResult_IsReturned_AndCached()
  {
    var enricher = CreateEnricher();
    SetupSearch("AAPL");

    var first = await enricher.GetEnrichmentAsync("aapl");
    Assert.Equal("Apple Inc.", first!.Name);
    Assert.Equal("Apple Inc. (AAPL)", first.LongName);
    Assert.Equal("Technology", first.Sector);
    Assert.Equal("Consumer Electronics", first.Industry);

    var second = await enricher.GetEnrichmentAsync("AAPL");
    Assert.Equal("Apple Inc.", second!.Name);

    _apiMock.Verify(m => m.SearchAsync("AAPL", 1, 0, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task UnknownTicker_IsNegativelyCached()
  {
    var enricher = CreateEnricher();
    SetupSearch("NVDA", symbol: "AAPL", name: "Apple Inc.");

    var first = await enricher.GetEnrichmentAsync("NVDA");
    Assert.Null(first);

    var second = await enricher.GetEnrichmentAsync("NVDA");
    Assert.Null(second);

    _apiMock.Verify(m => m.SearchAsync("NVDA", 1, 0, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task ApiFailure_IsNegativelyCached()
  {
    var enricher = CreateEnricher();
    _apiMock
      .Setup(m => m.SearchAsync("AAPL", 1, 0, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new HttpRequestException("boom"));

    Assert.Null(await enricher.GetEnrichmentAsync("AAPL"));
    Assert.Null(await enricher.GetEnrichmentAsync("AAPL"));

    _apiMock.Verify(m => m.SearchAsync("AAPL", 1, 0, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Theory]
  [InlineData("EQUITY", "Stock")]
  [InlineData("ETF", "Etf")]
  [InlineData("BOND", "Bond")]
  [InlineData("FUTURE", "Future")]
  [InlineData("CRYPTOCURRENCY", "Crypto")]
  [InlineData("MUTUALFUND", null)]
  [InlineData("CURRENCY", null)]
  [InlineData(null, null)]
  public async Task QuoteType_IsMappedToInstrumentType(string? quoteType, string? expectedType)
  {
    var enricher = CreateEnricher();
    SetupSearch("AAPL", quoteType: quoteType);

    var result = await enricher.GetEnrichmentAsync("AAPL");

    Assert.Equal(expectedType, result!.Type);
  }

  [Fact]
  public async Task DeepProfile_MergesSummaryProfile()
  {
    var enricher = CreateEnricher(deepProfile: true);
    _cache.Add("yahoo:crumb", "TESTCRUMB", DateTimeOffset.UtcNow.AddHours(1));
    SetupSearch("AAPL");
    _apiMock
      .Setup(m => m.GetQuoteSummaryAsync("AAPL", "TESTCRUMB", "summaryProfile", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new YahooQuoteSummaryResponse
      {
        QuoteSummary = new YahooQuoteSummaryResponse.YahooQuoteSummaryWrapper
        {
          Result =
          [
            new YahooSummaryProfile
            {
              Country = "USA",
              Website = "https://www.apple.com",
              Sector = "Technology",
              Industry = "Consumer Electronics",
              LongBusinessSummary = "Apple designs and sells smartphones.",
              FullTimeEmployees = 161000
            }
          ]
        }
      });

    var result = await enricher.GetEnrichmentAsync("AAPL");

    Assert.Equal("USA", result!.Country);
    Assert.Equal("https://www.apple.com", result.Website);
    Assert.Equal(161000, result.Employees);
    Assert.Equal("Apple designs and sells smartphones.", result.Summary);
    Assert.Equal("Technology", result.Sector);
  }

  [Fact]
  public async Task DeepProfile_DisabledByDefault_SkipsQuoteSummary()
  {
    var enricher = CreateEnricher(deepProfile: false);
    SetupSearch("AAPL");

    var result = await enricher.GetEnrichmentAsync("AAPL");

    Assert.Equal("Apple Inc.", result!.Name);
    Assert.Null(result.Country);
    Assert.Null(result.Website);
    _apiMock.Verify(m => m.GetQuoteSummaryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  private YahooInstrumentEnricher CreateEnricher(bool deepProfile = false)
  {
    var options = Options.Create(new YahooEnricherOptions { DeepProfile = deepProfile });
    var crumbProvider = new YahooCrumbProvider(_apiMock.Object, _cache, new Mock<IHttpClientFactory>().Object, options, Logger.None);
    return new YahooInstrumentEnricher(_apiMock.Object, crumbProvider, _cache, options, Logger.None);
  }

  private void SetupSearch(string upperTicker, string symbol = "AAPL", string name = "Apple Inc.", string? quoteType = null) =>
    _apiMock
      .Setup(m => m.SearchAsync(upperTicker, 1, 0, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new YahooSearchResponse
      {
        Count = 1,
        Quotes =
        [
          new YahooSearchQuote
          {
            Symbol = symbol,
            Shortname = name,
            Longname = $"{name} ({symbol})",
            Sector = "Technology",
            Industry = "Consumer Electronics",
            QuoteType = quoteType
          }
        ]
      });


  /// <summary>Process-local IAppCache (LazyCache CachingService storage is global and leaks between tests).</summary>
  private sealed class FakeAppCache : IAppCache
  {
    private readonly Dictionary<string, object?> _store = new();

    public ICacheProvider CacheProvider => throw new NotSupportedException();

    public CacheDefaults DefaultCachePolicy => new();

    public void Add<T>(string key, T value, MemoryCacheEntryOptions options) => _store[key] = value;

    public T? Get<T>(string key) => _store.TryGetValue(key, out var value) ? (T?)value : default;

    public Task<T> GetAsync<T>(string key) => Task.FromResult(Get<T>(key)!);

    public T GetOrAdd<T>(string key, Func<ICacheEntry, T> factory) => throw new NotSupportedException();

    public T GetOrAdd<T>(string key, Func<ICacheEntry, T> factory, MemoryCacheEntryOptions options) => throw new NotSupportedException();

    public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> factory) => throw new NotSupportedException();

    public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> factory, MemoryCacheEntryOptions options) => throw new NotSupportedException();

    public void Remove(string key) => _store.Remove(key);

    public bool TryGetValue<T>(string key, out T value)
    {
      if (_store.TryGetValue(key, out var raw) && raw is T typed)
      {
        value = typed;
        return true;
      }

      value = default!;
      return false;
    }
  }
}
