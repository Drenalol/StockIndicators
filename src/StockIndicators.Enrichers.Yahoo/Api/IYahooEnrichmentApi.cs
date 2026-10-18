using Refit;
using StockIndicators.Enrichers.Yahoo.Schema;

namespace StockIndicators.Enrichers.Yahoo.Api;

public interface IYahooEnrichmentApi
{
  [Get("/v1/finance/search")]
  Task<YahooSearchResponse> SearchAsync([Query] string q, [Query] int quotesCount = 1, [Query] int newsCount = 0, CancellationToken cancellationToken = default);

  [Get("/v1/test/getcrumb")]
  Task<string> GetCrumbAsync(CancellationToken cancellationToken = default);

  [Get("/v10/finance/quoteSummary/{symbol}")]
  Task<YahooQuoteSummaryResponse> GetQuoteSummaryAsync(string symbol, [Query] string crumb, [Query] string modules = "summaryProfile", CancellationToken cancellationToken = default);
}
