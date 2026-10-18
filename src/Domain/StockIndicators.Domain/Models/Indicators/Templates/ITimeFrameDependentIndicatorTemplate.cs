using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Domain.Models.Indicators.Templates;

public interface ITimeFrameDependentIndicatorTemplate : IIndicatorTemplate
{
  public TimeFrame TimeFrame { get; set; }
}