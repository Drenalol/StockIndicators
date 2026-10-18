namespace StockIndicators.Domain.Models.Indicators;

/// <summary>Unit of the trailing stop distance: a percent of the anchor extreme or a fixed price amount.</summary>
public enum TrailingThresholdType
{
  Percent = 0,
  Absolute
}
