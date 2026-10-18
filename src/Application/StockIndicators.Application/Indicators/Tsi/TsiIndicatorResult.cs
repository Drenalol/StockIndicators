using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Tsi;

public class TsiIndicatorResult : IndicatorResult
{
  public double Tsi { get; set; }
  public double Signal { get; set; }
  public TsiTrend Trend { get; set; }
}

public enum TsiTrend
{
  None = 0,
  Bullish,
  Bearish
}
