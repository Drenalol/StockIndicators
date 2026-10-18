using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Vortex;

public class VortexIndicatorResult : IndicatorResult
{
  public double Pvi { get; set; }
  public double Nvi { get; set; }
  public VortexTrend Trend { get; set; }
}

public enum VortexTrend
{
  None = 0,
  BullishCross,
  BearishCross
}
