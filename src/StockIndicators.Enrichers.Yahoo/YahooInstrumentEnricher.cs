using LazyCache;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Instruments.Enrichers;
using StockIndicators.Enrichers.Yahoo.Api;
using StockIndicators.Enrichers.Yahoo.Internal;
using StockIndicators.Enrichers.Yahoo.Schema;

namespace StockIndicators.Enrichers.Yahoo;

public class YahooInstrumentEnricher : IInstrumentEnricher
{
  private readonly IYahooEnrichmentApi _api;
  private readonly YahooCrumbProvider _crumbProvider;
  private readonly IAppCache _cache;
  private readonly YahooEnricherOptions _options;
  private readonly ILogger _logger;

  public YahooInstrumentEnricher(
    IYahooEnrichmentApi api,
    YahooCrumbProvider crumbProvider,
    IAppCache cache,
    IOptions<YahooEnricherOptions> options,
    ILogger logger
  )
  {
    _api = api;
    _crumbProvider = crumbProvider;
    _cache = cache;
    _options = options.Value;
    _logger = logger;
  }

  public async Task<InstrumentEnrichment?> GetEnrichmentAsync(string ticker, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(ticker))
      return null;

    var upper = ticker.ToUpperInvariant();
    var cacheKey = $"yahoo:enrich:{upper}";
    var missKey = $"yahoo:enrich:{upper}:miss";

    if (_cache.TryGetValue(missKey, out bool _))
      return null;

    if (await _cache.GetAsync<InstrumentEnrichment>(cacheKey) is { } cached)
      return cached;

    InstrumentEnrichment? enrichment;
    try
    {
      enrichment = await BuildEnrichmentAsync(upper, cancellationToken);
    }
    catch (Exception e)
    {
      _logger.Warning(e, "Yahoo enrichment for {Ticker} failed", upper);
      enrichment = null;
    }

    if (enrichment == null)
    {
      _cache.Add(missKey, true, DateTimeOffset.UtcNow.AddHours(_options.NegativeCacheTtlHours));
      return null;
    }

    _cache.Add(cacheKey, enrichment, DateTimeOffset.UtcNow.AddHours(_options.CacheTtlHours));
    return enrichment;
  }

  private async Task<InstrumentEnrichment?> BuildEnrichmentAsync(string upper, CancellationToken cancellationToken)
  {
    var search = await _api.SearchAsync(upper, 1, 0, cancellationToken);
    var quote = search.Quotes.FirstOrDefault(q => string.Equals(q.Symbol, upper, StringComparison.OrdinalIgnoreCase));

    if (quote == null || string.IsNullOrWhiteSpace(quote.Shortname))
      return null;

    var enrichment = new InstrumentEnrichment
    {
      Name = quote.Shortname,
      LongName = quote.Longname,
      Sector = quote.Sector,
      Industry = quote.Industry,
      Type = ToInstrumentType(quote.QuoteType)
    };

    if (!_options.DeepProfile)
      return enrichment;

    var profile = await GetSummaryProfileAsync(quote.Symbol!, cancellationToken);
    if (profile == null)
      return enrichment;

    return enrichment with
    {
      Country = profile.Country,
      Website = profile.Website,
      Sector = profile.Sector ?? enrichment.Sector,
      Industry = profile.Industry ?? enrichment.Industry,
      Employees = profile.FullTimeEmployees,
      Summary = profile.LongBusinessSummary
    };
  }

  // Yahoo quoteType values that map onto our instrument vocabulary; the rest
  // (MUTUALFUND, CURRENCY, INDEX, ...) has no category here, so stay null.
  private static string? ToInstrumentType(string? quoteType) => quoteType switch
  {
    "EQUITY" => "Stock",
    "ETF" => "Etf",
    "BOND" => "Bond",
    "FUTURE" => "Future",
    "CRYPTOCURRENCY" => "Crypto",
    _ => null
  };

  private async Task<YahooSummaryProfile?> GetSummaryProfileAsync(string symbol, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < 2; attempt++)
    {
      var crumb = await _crumbProvider.GetCrumbAsync(cancellationToken);
      var response = await _api.GetQuoteSummaryAsync(symbol, crumb, "summaryProfile", cancellationToken);
      var wrapper = response.QuoteSummary;

      if (wrapper?.Result is { Length: > 0 })
        return wrapper.Result[0];

      if (wrapper?.Error is { Description: "Invalid Crumb" })
      {
        _logger.Debug("Yahoo crumb rejected, refreshing");
        await _crumbProvider.InvalidateAsync(cancellationToken);
        continue;
      }

      return null;
    }

    return null;
  }
}
