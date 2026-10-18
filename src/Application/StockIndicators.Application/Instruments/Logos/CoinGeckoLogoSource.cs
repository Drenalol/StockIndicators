using System.Text.Json;
using LazyCache;
using Microsoft.Extensions.Options;
using Serilog;

namespace StockIndicators.Application.Instruments.Logos;

/// <summary>
/// Third cascade source (crypto only, after Logo.dev missed): CoinGecko. The candidate coin ids
/// behind a base asset come from the full coins list (cached 7 days) and are resolved through
/// one batched /coins/markets call: a coin is only accepted when its own symbol matches, and
/// among the matches the best market cap rank wins - the id list is ordered by id, so a meme
/// clone listed before the canonical coin ("batcat" with symbol btc before "bitcoin") cannot
/// take its logo. The search endpoint is the last resort (it covers coins whose symbol changed
/// upstream, e.g. Toncoin rebranded to GRAM) and ranks its matches the same way. Requests are
/// cheap and rare: every resolved logo is cached in Redis, and 429 from the public tier is
/// logged and cached as negative for a day instead of hammering the rate limit.
/// </summary>
public class CoinGeckoLogoSource : IInstrumentLogoSource
{
  internal const string CoinsListKey = "coingecko:coins-list";

  private static readonly TimeSpan CoinsListTtl = TimeSpan.FromDays(7);
  private const int MarketBatchSize = 100;

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly LogoOptions _options;
  private readonly IAppCache _cache;
  private readonly ILogger _logger;

  public CoinGeckoLogoSource(
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

  public async Task<InstrumentLogoData?> TryResolveAsync(InstrumentLogoRequest request, CancellationToken cancellationToken)
  {
    if (!request.IsCrypto)
      return null;

    var symbol = request.BaseAsset.ToLowerInvariant();
    var http = _httpClientFactory.CreateClient(LogoHttpClients.Client);

    var ids = (await GetCoinIdsBySymbolAsync(http, cancellationToken)).TryGetValue(symbol, out var known) ? known : [];

    return await ResolveByMarketRankAsync(http, ids, symbol, cancellationToken)
           ?? await ResolveBySearchAsync(http, symbol, cancellationToken);
  }

  /// <summary>
  /// A symbol-matched coin from /coins/markets or /search; Rank is null when CoinGecko does not rank it.
  /// </summary>
  public sealed record CoinCandidate(string Id, int? Rank, string ImageUrl);

  /// <summary>Best market cap rank wins; unrated clones sort after every ranked coin, then by id.</summary>
  public static IReadOnlyList<CoinCandidate> OrderByRank(IReadOnlyList<CoinCandidate> coins) =>
    coins.OrderBy(c => c.Rank ?? int.MaxValue).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

  private async Task<InstrumentLogoData?> ResolveByMarketRankAsync(HttpClient http, IReadOnlyList<string> ids, string symbol, CancellationToken cancellationToken)
  {
    var matches = new List<CoinCandidate>();

    foreach (var batch in ids.Chunk(MarketBatchSize))
    {
      var url = $"{_options.CoinGeckoBaseUrl.TrimEnd('/')}/coins/markets?vs_currency=usd&ids={Uri.EscapeDataString(string.Join(",", batch))}";

      using var response = await GetAsync(http, url, cancellationToken);

      if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
      {
        _logger.Warning("CoinGecko rate limit hit while resolving market ranks for {Symbol}", symbol);
        return null;
      }

      if (!response.IsSuccessStatusCode)
        continue;

      using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));

      // A coin answers only for its own symbol; ids that merely contain the ticker are rejected.
      foreach (var coin in document.RootElement.EnumerateArray())
      {
        if (coin.GetProperty("symbol").GetString() is not { } coinSymbol ||
            !string.Equals(coinSymbol, symbol, StringComparison.OrdinalIgnoreCase))
          continue;

        if (coin.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.String &&
            image.GetString() is { Length: > 0 } imageUrl)
          matches.Add(new CoinCandidate(coin.GetProperty("id").GetString() ?? "", RankOf(coin), imageUrl));
      }
    }

    foreach (var match in OrderByRank(matches))
    {
      var data = await LogoDownloads.DownloadAsync(http, new Uri(match.ImageUrl), "coingecko", cancellationToken);

      if (data is not null)
        return data;
    }

    return null;
  }

  private async Task<InstrumentLogoData?> ResolveBySearchAsync(HttpClient http, string symbol, CancellationToken cancellationToken)
  {
    using var response = await GetAsync(http, $"{_options.CoinGeckoBaseUrl.TrimEnd('/')}/search?query={Uri.EscapeDataString(symbol)}", cancellationToken);

    if (!response.IsSuccessStatusCode)
      return null;

    using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));

    if (!document.RootElement.TryGetProperty("coins", out var coins) || coins.ValueKind != JsonValueKind.Array)
      return null;

    var matches = new List<CoinCandidate>();

    foreach (var coin in coins.EnumerateArray())
    {
      if (coin.GetProperty("symbol").GetString() is not { } coinSymbol ||
          !string.Equals(coinSymbol, symbol, StringComparison.OrdinalIgnoreCase))
        continue;

      if (coin.TryGetProperty("thumb", out var thumb) && thumb.ValueKind == JsonValueKind.String &&
          thumb.GetString() is { Length: > 0 } thumbUrl)
        matches.Add(new CoinCandidate(coin.GetProperty("id").GetString() ?? "", RankOf(coin), thumbUrl));
    }

    foreach (var match in OrderByRank(matches))
    {
      var data = await LogoDownloads.DownloadAsync(http, new Uri(match.ImageUrl), "coingecko", cancellationToken);

      if (data is not null)
        return data;
    }

    return null;
  }

  private static int? RankOf(JsonElement coin) =>
    coin.TryGetProperty("market_cap_rank", out var rank) && rank.ValueKind == JsonValueKind.Number ? rank.GetInt32() : null;

  private async Task<Dictionary<string, List<string>>> GetCoinIdsBySymbolAsync(HttpClient http, CancellationToken cancellationToken) =>
    await _cache.GetOrAddAsync(
      CoinsListKey,
      async () =>
      {
        using var response = await GetAsync(http, $"{_options.CoinGeckoBaseUrl.TrimEnd('/')}/coins/list", cancellationToken);

        if (!response.IsSuccessStatusCode)
          throw new HttpRequestException($"CoinGecko coins list failed: {(int)response.StatusCode}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var coin in document.RootElement.EnumerateArray())
        {
          var id = coin.GetProperty("id").GetString();
          var symbol = coin.GetProperty("symbol").GetString();

          if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(symbol))
            continue;

          symbol = symbol.ToLowerInvariant();

          if (!result.TryGetValue(symbol, out var ids))
            result[symbol] = ids = [];

          ids.Add(id);
        }

        _logger.Information("CoinGecko coins list cached: {Count} symbols", result.Count);
        return result;
      },
      DateTimeOffset.UtcNow + CoinsListTtl
    );

  private async Task<HttpResponseMessage> GetAsync(HttpClient http, string url, CancellationToken cancellationToken)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, url);

    // CoinGecko's CDN rejects the default .NET client signature on the bulk endpoints with 403,
    // while any identified client passes; the shared "instrument-logos" client stays UA-neutral
    // for the other sources.
    request.Headers.UserAgent.ParseAdd("StockIndicators/1.0 (self-hosted; https://github.com/Drenalol/StockIndicators)");

    if (!string.IsNullOrWhiteSpace(_options.CoinGeckoApiKey))
      request.Headers.Add("x-cg-demo-api-key", _options.CoinGeckoApiKey);

    return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
  }
}
