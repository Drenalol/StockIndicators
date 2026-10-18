using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StockIndicators.Contracts.Rules;

[JsonConverter(typeof(StringEnumConverter))]
public enum ConditionOperatorDto
{
  CrossesAbove,
  CrossesBelow,
  GreaterThan,
  LessThan,
  Equal,
  GreaterThanOrEqual,
  LessThanOrEqual,
  WithinDeviation
}
