using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Cci;

public class CciIndicatorResult : IndicatorResult
{
  public double Cci { get; set; }
  public ZoneState State { get; set; }
}
