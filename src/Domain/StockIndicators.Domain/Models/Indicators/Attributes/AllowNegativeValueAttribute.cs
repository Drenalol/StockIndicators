namespace StockIndicators.Domain.Models.Indicators.Attributes;

/// <summary>
/// Marks a numeric template parameter whose meaningful values are negative
/// (e.g. Williams %R thresholds, which live on the -100..0 scale). Such a parameter
/// skips the default "must be greater than 0" rule and only rejects 0.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AllowNegativeValueAttribute : Attribute;
