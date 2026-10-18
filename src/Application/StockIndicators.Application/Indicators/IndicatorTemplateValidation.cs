using System.Globalization;
using System.Reflection;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Domain.Models.Indicators.Attributes;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators;

/// <summary>
/// Validates the numeric parameters of an indicator template. A zero or negative value
/// silently breaks the indicator math (division by zero, an always-true gap condition,
/// a period of 0), so every numeric parameter must be positive unless it is a tolerance
/// or channel bound where 0 is a meaningful setting.
/// </summary>
public static class IndicatorTemplateValidation
{
  // Tolerances and channel bounds where 0 is a valid setting
  private static readonly HashSet<string> ZeroAllowed =
  [
    nameof(MaIndicatorTemplate.Deviation),
    nameof(StochRsiIndicatorTemplate.MinChannel),
    nameof(StochRsiIndicatorTemplate.MaxChannel)
  ];

  private static readonly HashSet<Type> IntegerTypes = [typeof(int), typeof(long)];
  private static readonly HashSet<Type> RealTypes = [typeof(float), typeof(double), typeof(decimal)];

  /// <summary>
  /// Returns one structured <see cref="ValidationError"/> per offending numeric parameter. The
  /// English <see cref="ValidationError.Message"/> is kept for logs and plain-string API consumers,
  /// while the code + args let clients render a localized message.
  /// </summary>
  public static IReadOnlyList<ValidationError> GetErrors(IIndicatorTemplate template)
  {
    var errors = new List<ValidationError>();

    foreach (var property in template.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      if (!property.CanRead)
        continue;

      var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

      if (!IntegerTypes.Contains(type) && !RealTypes.Contains(type))
        continue;

      if (property.GetValue(template) is not IConvertible convertible)
        continue;

      var value = convertible.ToDouble(CultureInfo.InvariantCulture);

      // Thresholds that live on a negative scale (Williams %R) carry no positivity bound.
      if (property.GetCustomAttribute<AllowNegativeValueAttribute>() is not null)
        continue;

      if (IntegerTypes.Contains(type))
      {
        if (value < 1)
          errors.Add(new ValidationError(
            "template.paramMin",
            $"'{property.Name}' must be at least 1",
            new Dictionary<string, object?> { ["parameter"] = property.Name, ["min"] = 1 }));
      }
      else if (ZeroAllowed.Contains(property.Name))
      {
        if (value < 0)
          errors.Add(new ValidationError(
            "template.paramNonNegative",
            $"'{property.Name}' must not be negative",
            new Dictionary<string, object?> { ["parameter"] = property.Name }));
      }
      else
      {
        if (value <= 0)
          errors.Add(new ValidationError(
            "template.paramPositive",
            $"'{property.Name}' must be greater than 0",
            new Dictionary<string, object?> { ["parameter"] = property.Name }));
      }
    }

    return errors;
  }
}
