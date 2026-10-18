using System.Reflection;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Attributes;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class IndicatorTemplateHintTests
{
  [Fact]
  public void EveryTemplatePropertyHasLocalizedHint()
  {
    foreach (var indicator in Enum.GetValues<Indicator>())
    {
      var template = indicator.GetTemplate();
      var properties = template.GetProperties();

      var missing = properties
        .Where(prop => prop.GetCustomAttribute<ParameterHintAttribute>() is null)
        .Select(prop => prop.Name)
        .ToList();

      Assert.True(
        missing.Count == 0,
        $"{template.Name} (indicator {indicator}) is missing hints for: {string.Join(", ", missing)}");

      foreach (var prop in properties)
      {
        var hint = prop.GetCustomAttribute<ParameterHintAttribute>()!;
        Assert.False(string.IsNullOrWhiteSpace(hint.GetText("en")), $"{template.Name}.{prop.Name} has an empty En hint");
        // An unsupported language must fall back instead of returning nothing.
        Assert.False(string.IsNullOrWhiteSpace(hint.GetText("zz")), $"{template.Name}.{prop.Name} has no fallback for unsupported languages");
        AssertEveryRegistryLanguageIsTranslated(hint, $"{template.Name}.{prop.Name}");
      }
    }
  }

  [Fact]
  public void EveryTemplateHasLocalizedIndicatorHint()
  {
    foreach (var indicator in Enum.GetValues<Indicator>())
    {
      var template = indicator.GetTemplate();
      var hint = template.GetCustomAttribute<IndicatorHintAttribute>();

      Assert.NotNull(hint);
      Assert.False(string.IsNullOrWhiteSpace(hint!.GetText("en")), $"{template.Name} (indicator {indicator}) has an empty En indicator hint");
      Assert.False(string.IsNullOrWhiteSpace(hint.GetText("zz")), $"{template.Name} (indicator {indicator}) has no fallback for unsupported languages");
      AssertEveryRegistryLanguageIsTranslated(hint, template.Name);
    }
  }

  /// <summary>
  /// Every language in the <see cref="UserLocale"/> registry must carry its own text:
  /// an untranslated language silently falls back to English, so its text would read
  /// exactly as the En one - the same "missing translation" signal the catalog test uses.
  /// </summary>
  private static void AssertEveryRegistryLanguageIsTranslated(LocalizedTextAttributeBase hint, string what)
  {
    var english = hint.GetText("en")!;

    foreach (var descriptor in UserLocale.SupportedLanguages)
    {
      if (descriptor.Code == "en")
        continue;

      var text = hint.GetText(descriptor.Code);
      Assert.False(string.IsNullOrWhiteSpace(text), $"{what} has an empty {descriptor.Code} hint");
      Assert.False(
        string.Equals(english, text, StringComparison.Ordinal),
        $"{what} has no {descriptor.Code} translation (falls back to English)");
    }
  }

  [Fact]
  public void UnsupportedLanguageFallsBackToEnglish()
  {
    var hint = new ParameterHintAttribute("english text", "русский текст");

    Assert.Equal("english text", hint.GetText("zz"));
    Assert.Equal("english text", hint.GetText(null));
    Assert.Equal("русский текст", hint.GetText("ru"));
  }
}
