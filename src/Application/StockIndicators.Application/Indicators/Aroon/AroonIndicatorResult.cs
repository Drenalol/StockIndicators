using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Aroon;

public class AroonIndicatorResult : IndicatorResult
{
  public double AroonUp { get; set; }
  public double AroonDown { get; set; }
  public double Oscillator { get; set; }
  public AroonTrend Trend { get; set; }
}

public enum AroonTrend
{
  None = 0,
  BullishCross,
  BearishCross
}
