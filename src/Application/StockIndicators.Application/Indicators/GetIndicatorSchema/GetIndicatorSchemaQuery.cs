using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.GetIndicatorSchema;

/// <summary>
/// Template schema of an indicator, localized ("ru" or "en"): description,
/// property types, defaults and parameter hints. Shared by the web API and the
/// Telegram bot indicator docs.
/// </summary>
public record GetIndicatorSchemaQuery(Indicator Indicator, string Language) : IQuery<IndicatorSchemaDto>;

public record IndicatorSchemaDto(
  string Name,
  string? Description,
  IDictionary<string, string> Properties,
  IDictionary<string, string> Defaults,
  IDictionary<string, string> Descriptions,
  IDictionary<string, string[]> EnumOptions);
