using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Psar;

public class PsarIndicatorResult : IndicatorResult
{
  public double Sar { get; set; }
  public PsarDirection Direction { get; set; }
}

public enum PsarDirection
{
  Bullish = 0,
  Bearish
}
