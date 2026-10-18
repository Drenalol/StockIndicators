namespace StockIndicators.Domain.Models.Indicators.Templates;

public interface ICandleCountDependentIndicatorTemplate : IIndicatorTemplate
{
  public int GetCandleCount();
}