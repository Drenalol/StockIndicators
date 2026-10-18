using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Cmo;

public class CmoIndicatorResult : IndicatorResult
{
  public double Cmo { get; set; }
  public ZoneState State { get; set; }
}
