using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Chandelier;

public class ChandelierIndicatorResult : IndicatorResult
{
  public double Line { get; set; }
  public ChandelierSide Side { get; set; }
}

public enum ChandelierSide
{
  None = 0,
  Above,
  Below
}
