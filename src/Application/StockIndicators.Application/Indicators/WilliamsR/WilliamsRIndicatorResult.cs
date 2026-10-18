using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.WilliamsR;

public class WilliamsRIndicatorResult : IndicatorResult
{
  public double WilliamsR { get; set; }
  public ZoneState State { get; set; }
}
