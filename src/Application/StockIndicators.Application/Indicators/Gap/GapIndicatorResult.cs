using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Gap;

public class GapIndicatorResult : IndicatorResult
{
  public DateTime Previous { get; set; }
  public double Percent { get; set; }
}