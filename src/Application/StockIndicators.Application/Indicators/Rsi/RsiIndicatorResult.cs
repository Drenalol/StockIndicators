using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Rsi;

public class RsiIndicatorResult : IndicatorResult
{
  public double Rsi { get; set; }
  public RsiState State { get; set; }
}

public enum RsiState
{
  Neutral,
  Overbought,
  Oversold
}
