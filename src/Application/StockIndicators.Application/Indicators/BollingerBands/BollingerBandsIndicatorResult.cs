using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.BollingerBands;

public class BollingerBandsIndicatorResult : IndicatorResult
{
  public double UpperBand { get; set; }
  public double MiddleBand { get; set; }
  public double LowerBand { get; set; }
  public double PercentB { get; set; }
  public double Width { get; set; }
  public BbPosition Position { get; set; }
}

public enum BbPosition
{
  Inside,
  AboveUpper,
  BelowLower,
  TouchingUpper,
  TouchingLower
}
