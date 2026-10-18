namespace StockIndicators.Domain.Models.Indicators.Attributes;

/// <summary>
/// Localized "what is this indicator" description for a template class.
/// Not inherited: a derived template (e.g. EmaIndicatorTemplate : MaIndicatorTemplate)
/// must declare its own hint instead of silently reusing the base one.
///
/// Per-language texts are named properties, not positional constructor parameters:
/// adding a language never breaks existing usages. A language with no property here
/// falls back to <see cref="LocalizedTextAttributeBase.FallbackLanguage"/> (English by default) at localization time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class IndicatorHintAttribute : LocalizedTextAttributeBase
{
  public IndicatorHintAttribute()
  {
  }

  public IndicatorHintAttribute(string en, string ru) : base(en, ru)
  {
  }
}
