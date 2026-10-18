namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Comparison operators for rule conditions.
/// Used to compare indicator values against reference values.
/// </summary>
public enum ConditionOperator
{
  /// <summary>Indicator value crosses above the reference</summary>
  CrossesAbove,

  /// <summary>Indicator value crosses below the reference</summary>
  CrossesBelow,

  /// <summary>Indicator value is greater than the reference</summary>
  GreaterThan,

  /// <summary>Indicator value is less than the reference</summary>
  LessThan,

  /// <summary>Indicator value is equal to the reference</summary>
  Equal,

  /// <summary>Indicator value is greater than or equal to the reference</summary>
  GreaterThanOrEqual,

  /// <summary>Indicator value is less than or equal to the reference</summary>
  LessThanOrEqual,

  /// <summary>Indicator value is within a deviation range</summary>
  WithinDeviation
}
