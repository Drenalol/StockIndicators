using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class MaIndicatorResult : IndicatorResult
{
  public MaTest Test { get; set; }
  public double Result { get; set; }
}