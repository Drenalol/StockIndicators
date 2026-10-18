using LazyCache;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Enrichers.Yahoo.Api;

namespace StockIndicators.Enrichers.Yahoo.Internal;

/// <summary>
/// Manages the Yahoo cookie + crumb session required by quoteSummary endpoint.
/// fc.yahoo.com responds 404 but sets the A3 cookie; getcrumb returns the crumb
/// string that must accompany subsequent quoteSummary requests.
/// </summary>
public class YahooCrumbProvider
{
  private const string CrumbCacheKey = "yahoo:crumb";
  private const string CookieBootstrapUrl = "https://fc.yahoo.com";
  private static readonly TimeSpan CrumbTtl = TimeSpan.FromHours(6);

  private readonly IYahooEnrichmentApi _api;
  private readonly IAppCache _cache;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger _logger;
  private readonly SemaphoreSlim _lock = new(1, 1);

  public YahooCrumbProvider(
    IYahooEnrichmentApi api,
    IAppCache cache,
    IHttpClientFactory httpClientFactory,
    IOptions<YahooEnricherOptions> options,
    ILogger logger
  )
  {
    _api = api;
    _cache = cache;
    _httpClientFactory = httpClientFactory;
    _logger = logger;
  }

  public async Task<string> GetCrumbAsync(CancellationToken cancellationToken = default)
  {
    var cached = await _cache.GetAsync<string>(CrumbCacheKey);
    if (cached != null)
      return cached;

    await _lock.WaitAsync(cancellationToken);
    try
    {
      cached = await _cache.GetAsync<string>(CrumbCacheKey);
      if (cached != null)
        return cached;

      await BootstrapCookiesAsync(cancellationToken);

      var crumb = (await _api.GetCrumbAsync(cancellationToken)).Trim();
      _cache.Add(CrumbCacheKey, crumb, DateTimeOffset.UtcNow.Add(CrumbTtl));
      return crumb;
    }
    finally
    {
      _lock.Release();
    }
  }

  public Task InvalidateAsync(CancellationToken cancellationToken = default)
  {
    _cache.Remove(CrumbCacheKey);
    return Task.CompletedTask;
  }

  private async Task BootstrapCookiesAsync(CancellationToken cancellationToken)
  {
    try
    {
      var client = _httpClientFactory.CreateClient("yahoo");
      await client.GetAsync(CookieBootstrapUrl, cancellationToken);
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Yahoo cookie bootstrap failed, crumb flow may be unavailable");
    }
  }
}
