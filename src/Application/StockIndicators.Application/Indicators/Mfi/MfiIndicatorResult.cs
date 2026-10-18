using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Mfi;

public class MfiIndicatorResult : IndicatorResult
{
  public double Mfi { get; set; }
  public ZoneState State { get; set; }
}
