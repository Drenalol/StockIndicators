using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.PriceChange;

public class PriceChangeIndicatorResult : IndicatorResult
{
  public double Change { get; set; }
  public bool Above { get; set; }
}
