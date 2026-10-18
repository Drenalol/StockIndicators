using System.Text;
using LazyCache;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog.Core;
using StockIndicators.Abstract;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Infrastructure.Domain.Candles;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests.Common;

[CollectionDefinition(nameof(SharedContext))]
public class SharedContext : ICollectionFixture<IndicatorCacheFixture>, ICollectionFixture<CandleRepositoryFixture>;

public class IndicatorCacheFixture
{
  public readonly IIndicatorCache IndicatorCache;

  public IndicatorCacheFixture()
  {
    IndicatorCache = new IndicatorInMemoryCache(new CachingService());
  }
}

public class CandleRepositoryFixture : IAsyncLifetime
{
  public ICandleRepository CandleRepository = null!;

  public async Task InitializeAsync()
  {
    var candleCache = new CandleInMemoryCache(new CachingService(), new NoopCandleStore(), Logger.None);

    var candles = await GetFromEmbeddedResourceAsync().ToArrayAsync();

    await candleCache.AddRangeAsync(candles, false);

    CandleRepository = new CachedCandleRepository(candleCache, [], Logger.None);

    async IAsyncEnumerable<Candle> GetFromEmbeddedResourceAsync()
    {
      var provider = new EmbeddedFileProvider(typeof(CandleRepositoryFixture).Assembly, "StockIndicators.Indicators.Tests.Common");
      var jsonSerializer = new JsonSerializer
      {
        ContractResolver = new DefaultContractResolver
        {
          NamingStrategy = new SnakeCaseNamingStrategy()
        }
      };

      foreach (var file in provider.GetDirectoryContents("/"))
      {
        await using var stream = file.CreateReadStream();
        using var streamReader = new StreamReader(stream, Encoding.UTF8);

        var dictionary = (Dictionary<DateTime, Candle>?)jsonSerializer.Deserialize(streamReader, typeof(Dictionary<DateTime, Candle>));

        if (dictionary == null)
          continue;

        foreach (var (_, candle) in dictionary)
          yield return candle;
      }
    }
  }

  public Task DisposeAsync() => Task.CompletedTask;
}

public sealed class NoopCandleStore : ICandleStore
{
  public Task<Dictionary<DateTime, Candle>?> LoadAsync(string key, CancellationToken cancellationToken = default) =>
    Task.FromResult<Dictionary<DateTime, Candle>?>(null);

  public Task SaveCandlesAsync(string key, IEnumerable<Candle> candles, TimeSpan ttl, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;
}