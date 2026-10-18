using System.Text.Json;
using LazyCache;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Instruments.Logos;

namespace StockIndicators.Application.Instruments.CryptoNames;

/// <summary>
/// CoinGecko-backed asset names: the same public coins list the logo source already
/// caches for a week, kept as symbol -> names ("btc" -> "bitcoin", "ethereum"...), plus
/// the two top-market-cap pages (/coins/markets) that mark the canonical coins of each
/// symbol. Names of every coin sharing a symbol are collected (clone names are noise but
/// harmless: the top-alias ranking tier prefers the canonical coin). Failures answer with
/// an empty map without poisoning the cache, so instrument warm-up never dies on a
/// CoinGecko hiccup.
/// </summary>
public class CoinGeckoCryptoAssetNames : ICryptoAssetNames
{
  internal const string NamesKey = "coingecko:coins-names";

  private static readonly TimeSpan NamesTtl = TimeSpan.FromDays(7);
  private const int MaxNamesPerSymbol = 25;

  /// <summary>Coins inside this market-cap rank supply the "top" aliases.</summary>
  internal const int TopMarketCapRank = 500;

  private const int MarketPageSize = 250;
  private const int MarketPages = TopMarketCapRank / MarketPageSize;

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly LogoOptions _options;
  private readonly IAppCache _cache;
  private readonly ILogger _logger;

  public CoinGeckoCryptoAssetNames(
    IHttpClientFactory httpClientFactory,
    IOptions<LogoOptions> options,
    IAppCache cache,
    ILogger logger)
  {
    _httpClientFactory = httpClientFactory;
    _options = options.Value;
    _cache = cache;
    _logger = logger;
  }

  public async Task<IReadOnlyDictionary<string, AssetNames>> GetNamesBySymbolAsync(CancellationToken cancellationToken)
  {
    try
    {
      return await _cache.GetOrAddAsync(
        NamesKey,
        () => LoadAsync(cancellationToken),
        DateTimeOffset.UtcNow + NamesTtl);
    }
    catch (Exception ex)
    {
      _logger.Warning(ex, "CoinGecko asset names unavailable; crypto aliases skipped for this run");
      return new Dictionary<string, AssetNames>();
    }
  }

  private async Task<Dictionary<string, AssetNames>> LoadAsync(CancellationToken cancellationToken)
  {
    var grouped = await LoadAllNamesAsync(cancellationToken);
    await AddTopNamesAsync(grouped, cancellationToken);

    _logger.Information("CoinGecko asset names cached: {Count} symbols", grouped.Count);

    return grouped.ToDictionary(
      pair => pair.Key,
      pair => new AssetNames(pair.Value.All, pair.Value.Top),
      StringComparer.OrdinalIgnoreCase);
  }

  private sealed class NamesBuilder
  {
    public List<string> All { get; } = [];
    public List<string> Top { get; } = [];
  }

  private async Task<Dictionary<string, NamesBuilder>> LoadAllNamesAsync(CancellationToken cancellationToken)
  {
    using var document = JsonDocument.Parse(await GetJsonAsync("/coins/list", cancellationToken));

    var grouped = new Dictionary<string, NamesBuilder>(StringComparer.OrdinalIgnoreCase);

    foreach (var coin in document.RootElement.EnumerateArray())
    {
      if (coin.GetProperty("name").GetString() is not { Length: > 0 } rawName ||
          coin.GetProperty("symbol").GetString() is not { Length: > 0 } rawSymbol)
        continue;

      var symbol = rawSymbol.ToLowerInvariant();
      var name = rawName.ToLowerInvariant();

      if (!grouped.TryGetValue(symbol, out var names))
        grouped[symbol] = names = new NamesBuilder();

      if (names.All.Count < MaxNamesPerSymbol && !names.All.Contains(name))
        names.All.Add(name);
    }

    return grouped;
  }

  // The market pages rank coins by market cap: a name coming from there is the canonical
  // asset of its symbol ("bitcoin" for btc), not a clone that borrowed the ticker.
  private async Task AddTopNamesAsync(Dictionary<string, NamesBuilder> grouped, CancellationToken cancellationToken)
  {
    for (var page = 1; page <= MarketPages; page++)
    {
      JsonDocument document;

      try
      {
        document = JsonDocument.Parse(
          await GetJsonAsync($"/coins/markets?vs_currency=usd&order=market_cap_desc&per_page={MarketPageSize}&page={page}", cancellationToken));
      }
      catch (Exception ex)
      {
        // The full-name map is already built; without the top split every alias ranks equal
        _logger.Warning(ex, "CoinGecko market-cap page {Page} failed; top asset names incomplete", page);
        return;
      }

      using (document)
        foreach (var coin in document.RootElement.EnumerateArray())
        {
          if (coin.GetProperty("name").GetString() is not { Length: > 0 } rawName ||
              coin.GetProperty("symbol").GetString() is not { Length: > 0 } rawSymbol)
            continue;

          if (coin.TryGetProperty("market_cap_rank", out var rank) && rank.ValueKind == JsonValueKind.Number &&
              rank.GetInt32() > TopMarketCapRank)
            continue;

          var symbol = rawSymbol.ToLowerInvariant();
          var name = rawName.ToLowerInvariant();

          if (!grouped.TryGetValue(symbol, out var names))
            grouped[symbol] = names = new NamesBuilder();

          if (names.Top.Count < MaxNamesPerSymbol && !names.Top.Contains(name))
            names.Top.Add(name);
        }
    }
  }

  private async Task<string> GetJsonAsync(string path, CancellationToken cancellationToken)
  {
    var http = _httpClientFactory.CreateClient(LogoHttpClients.Client);

    var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.CoinGeckoBaseUrl.TrimEnd('/')}{path}");

    // CoinGecko's CDN rejects the default .NET client signature on the bulk endpoints with 403
    // (the same workaround as the coins list in CoinGeckoLogoSource)
    request.Headers.UserAgent.ParseAdd("StockIndicators/1.0 (self-hosted; https://github.com/Drenalol/StockIndicators)");

    if (!string.IsNullOrWhiteSpace(_options.CoinGeckoApiKey))
      request.Headers.Add("x-cg-demo-api-key", _options.CoinGeckoApiKey);

    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    if (!response.IsSuccessStatusCode)
      throw new HttpRequestException($"CoinGecko {path} failed: {(int)response.StatusCode}");

    return await response.Content.ReadAsStringAsync(cancellationToken);
  }
}
