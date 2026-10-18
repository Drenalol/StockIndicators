using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Vwap;

public class VwapIndicatorResult : IndicatorResult
{
  public double Vwap { get; set; }
  public VwapPosition Position { get; set; }
}

public enum VwapPosition
{
  Above,
  Below,
  On
}
