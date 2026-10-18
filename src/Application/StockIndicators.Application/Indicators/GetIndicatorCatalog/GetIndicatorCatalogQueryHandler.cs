using System.Reflection;
using LazyCache;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Application.Indicators.GetIndicatorCatalog;

public class GetIndicatorCatalogQueryHandler : IQueryHandler<GetIndicatorCatalogQuery, IndicatorCatalogDto[]>
{
  /// <summary>Derived from attributes only - deterministic per language, so a long TTL is safe.</summary>
  private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

  private readonly IAppCache _cache;

  public GetIndicatorCatalogQueryHandler(IAppCache cache)
  {
    _cache = cache;
  }

  public Task<IndicatorCatalogDto[]> Handle(GetIndicatorCatalogQuery request, CancellationToken cancellationToken) =>
    _cache.GetOrAddAsync(
      $"indicator-catalog:{request.Language}",
      () => Task.FromResult(Build(request.Language)),
      DateTimeOffset.UtcNow.Add(CacheTtl));

  private static IndicatorCatalogDto[] Build(string language) =>
    Enum.GetValues<Indicator>()
      .Select(indicator => new IndicatorCatalogDto(
        indicator.ToString(),
        indicator.GetTemplate().GetCustomAttribute<IndicatorHintAttribute>()?.GetText(language)))
      .ToArray();
}
