using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Indicators.GetIndicatorCatalog;

/// <summary>
/// The full indicator catalog localized into the query language: display key,
/// the localized "what is this" description. Serves both the
/// authenticated dashboard list and the public landing indicators page; it is
/// derived by reflection from the Indicator enum and its template attributes,
/// so a new indicator appears in every surface without touching this query.
/// </summary>
public record GetIndicatorCatalogQuery(string Language) : IQuery<IndicatorCatalogDto[]>;

public record IndicatorCatalogDto(
  string Name,
  string? Description);
