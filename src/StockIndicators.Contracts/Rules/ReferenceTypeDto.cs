using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StockIndicators.Contracts.Rules;

[JsonConverter(typeof(StringEnumConverter))]
public enum ReferenceTypeDto
{
  Indicator,
  Price,
  Value
}
