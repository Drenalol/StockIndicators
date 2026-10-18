using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.SuperTrend;

public class SuperTrendIndicatorResult : IndicatorResult
{
  public double SuperTrend { get; set; }
  public double UpperBand { get; set; }
  public double LowerBand { get; set; }
  public SuperTrendDirection Direction { get; set; }
}

public enum SuperTrendDirection
{
  Bullish,
  Bearish
}
