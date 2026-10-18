using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Atr;

public class AtrIndicatorResult : IndicatorResult
{
  public double TrueRange { get; set; }
  public double Atr { get; set; }
  public double AtrPercent { get; set; }
}
