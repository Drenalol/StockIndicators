using System.Globalization;
using System.Reflection;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Application.Indicators.GetIndicatorSchema;

public class GetIndicatorSchemaQueryHandler : IQueryHandler<GetIndicatorSchemaQuery, IndicatorSchemaDto>
{
  public Task<IndicatorSchemaDto> Handle(GetIndicatorSchemaQuery request, CancellationToken cancellationToken)
  {
    var template = request.Indicator.GetTemplate();
    var hint = template.GetCustomAttribute<IndicatorHintAttribute>();
    var instance = Activator.CreateInstance(template)!;

    var schema = new IndicatorSchemaDto(
      request.Indicator.ToString(),
      hint?.GetText(request.Language),
      template
        .GetProperties()
        .ToDictionary(prop => prop.Name, prop => prop.PropertyType.Name),
      template
        .GetProperties()
        .ToDictionary(
          prop => prop.Name,
          prop => Convert.ToString(prop.GetValue(instance), CultureInfo.InvariantCulture) ?? string.Empty),
      template
        .GetProperties()
        .Where(prop => prop.GetCustomAttribute<ParameterHintAttribute>() is not null)
        .ToDictionary(
          prop => prop.Name,
          prop => prop.GetCustomAttribute<ParameterHintAttribute>()!.GetText(request.Language)!),
      // Names of custom template enums (Direction) so clients render a select without
      // hardcoding the options. TimeFrame and CandlePart are excluded: their pickers are
      // built-in widgets keyed by the parameter name on the client side.
      template
        .GetProperties()
        .Where(prop =>
          prop.PropertyType.IsEnum
          && prop.PropertyType != typeof(TimeFrame)
          && prop.PropertyType != typeof(CandlePart))
        .ToDictionary(prop => prop.Name, prop => Enum.GetNames(prop.PropertyType)));

    return Task.FromResult(schema);
  }
}
