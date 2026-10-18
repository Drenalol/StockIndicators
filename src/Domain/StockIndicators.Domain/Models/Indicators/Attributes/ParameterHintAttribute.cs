namespace StockIndicators.Domain.Models.Indicators.Attributes;

/// <summary>
/// Localized hint for one indicator template parameter. Texts are named properties,
/// not positional constructor parameters — see <see cref="LocalizedTextAttributeBase"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParameterHintAttribute : LocalizedTextAttributeBase
{
  public ParameterHintAttribute()
  {
  }

  public ParameterHintAttribute(string en, string ru) : base(en, ru)
  {
  }
}
