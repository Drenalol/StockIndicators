using Refit;
using StockIndicators.Providers.BusinessQuant.Schema;

namespace StockIndicators.Providers.BusinessQuant.Api;

public interface IBusinessQuantApi
{
  [Get("/quotes")]
  Task<BusinessQuantQuotesResponse> GetQuotesAsync(
    [Query, AliasAs("api_key")] string apiKey,
    [Query] string ticker,
    [Query] string mode,
    [Query, AliasAs("period")] string? period,
    [Query, AliasAs("from_date")] string? fromDate,
    [Query, AliasAs("till_date")] string? tillDate,
    [Query] int? limit,
    [Query] int? page = null,
    CancellationToken cancellationToken = default);

  [Get("/quotes")]
  Task<Dictionary<string, BusinessQuantQuotesResponse>> GetQuotesBatchAsync(
    [Query, AliasAs("api_key")] string apiKey,
    [Query] string ticker,
    [Query] string mode,
    [Query, AliasAs("period")] string period,
    [Query] int limit,
    CancellationToken cancellationToken = default);

  [Get("/universe")]
  Task<BusinessQuantUniverseResponse> GetUniverseAsync(
    [Query, AliasAs("api_key")] string apiKey,
    [Query, AliasAs("security_type")] string? securityType,
    CancellationToken cancellationToken = default);
}
