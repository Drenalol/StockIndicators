using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Obv;

public class ObvIndicatorResult : IndicatorResult
{
  public double Obv { get; set; }
  public double ObvEma { get; set; }
  public ObvTrend Trend { get; set; }
}

public enum ObvTrend
{
  None,
  Bullish,
  Bearish
}
