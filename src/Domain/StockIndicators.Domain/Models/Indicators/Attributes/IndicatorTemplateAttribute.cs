namespace StockIndicators.Domain.Models.Indicators.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class IndicatorTemplateAttribute : Attribute
{
  public Type Template { get; }

  public IndicatorTemplateAttribute(Type template) => Template = template;
}