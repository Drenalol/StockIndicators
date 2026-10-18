using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Macd;

public class MacdIndicatorResult : IndicatorResult
{
  public double Macd { get; set; }
  public double Signal { get; set; }
  public double Histogram { get; set; }
  public MacdTrend Trend { get; set; }
}

public enum MacdTrend
{
  None,
  Bullish,
  Bearish
}
