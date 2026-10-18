using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Volume;

public class VolumeIndicatorResult : IndicatorResult
{
  public decimal Volume { get; set; }
  public decimal Average { get; set; }
  public double Ratio { get; set; }
}
