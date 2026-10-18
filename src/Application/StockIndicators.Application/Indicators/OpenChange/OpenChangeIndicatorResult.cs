using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.OpenChange;

public class OpenChangeIndicatorResult : IndicatorResult
{
  public double Change { get; set; }
  public bool Above { get; set; }
}
