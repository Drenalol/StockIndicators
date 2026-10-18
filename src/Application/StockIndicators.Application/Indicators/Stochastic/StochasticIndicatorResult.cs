using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Stochastic;

public class StochasticIndicatorResult : IndicatorResult
{
  public double PercK { get; set; }
  public double PercD { get; set; }
  public double PercKPrevious { get; set; }
  public double PercDPrevious { get; set; }
  public StochTrend Trend { get; set; }
}

public enum StochTrend
{
  None,
  BullishCross,
  BearishCross,
  Overbought,
  Oversold
}
