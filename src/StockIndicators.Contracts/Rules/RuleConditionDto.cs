using Newtonsoft.Json.Linq;
using StockIndicators.Contracts.Candles;

namespace StockIndicators.Contracts.Rules;

/// <summary>
/// DTO for the rule editor. Represents a single alert condition (leaf of a rule group).
/// </summary>
public record RuleConditionDto(
  Guid? Id,
  string Indicator,
  JToken? TemplateData,
  ConditionOperatorDto Operator,
  ReferenceTypeDto ReferenceType,
  string? ReferenceIndicator,
  JToken? ReferenceTemplateData,
  CandlePartDto? ReferenceCandlePart,
  decimal? ReferenceValue,
  decimal? Deviation,
  string Ticker,
  string? ExchangeName,
  TimeFrame TimeFrame
);
