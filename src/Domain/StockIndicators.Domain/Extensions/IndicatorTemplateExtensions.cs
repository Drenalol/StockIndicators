using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Extensions;

public static class IndicatorTemplateExtensions
{
  public static Type GetTemplate(this Indicator indicator) => indicator.Get<IndicatorTemplateAttribute>().Template;
}