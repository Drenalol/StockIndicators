using System.Reflection;
using System.Text.RegularExpressions;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Attributes;
using Xunit;

namespace StockIndicators.Indicators.Tests.Locale;

/// <summary>
/// Machine-translation guard: none of the localized texts may contain CJK characters
/// (Han, kana, Hangul, full-width forms). Translations arrive from models and a stray
/// ideograph must fail here, not on a user's screen.
/// </summary>
public class NoCjkTextGuardTests
{
  // CJK radicals/ideographs (incl. Ext. A and CJK punctuation), kana, Hangul,
  // compatibility ideographs, full-width forms and the supplementary ideograph planes
  private static bool IsCjk(int codePoint) =>
    codePoint is >= 0x2E80 and <= 0x9FFF
      or >= 0xAC00 and <= 0xD7AF
      or >= 0xF900 and <= 0xFAFF
      or >= 0xFF00 and <= 0xFFEF
      or >= 0x20000 and <= 0x2FA1F;

  private static string? FirstCjk(string text)
  {
    for (var i = 0; i < text.Length; i++)
    {
      var codePoint = char.ConvertToUtf32(text, i);

      // walk over a whole surrogate pair (emoji like 📊 are valid, not CJK)
      if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
        i++;

      if (IsCjk(codePoint))
        return $"U+{codePoint:X4}";
    }

    return null;
  }

  [Fact]
  public void CatalogValuesContainNoCjkCharacters()
  {
    var offenders = new List<string>();

    foreach (var key in Enum.GetValues<LocalizationKey>())
    foreach (var descriptor in UserLocale.SupportedLanguages)
    {
      var offender = FirstCjk(LocalizationCatalog.Get(key, descriptor.Code));
      if (offender is not null)
        offenders.Add($"{key}/{descriptor.Code}: {offender}");
    }

    Assert.True(offenders.Count == 0, $"CJK characters in the catalog: {string.Join(", ", offenders)}");
  }

  [Fact]
  public void IndicatorTemplateHintsContainNoCjkCharacters()
  {
    var offenders = new List<string>();

    foreach (var indicator in Enum.GetValues<Indicator>())
    {
      var template = indicator.GetTemplate();

      var attributes = template
        .GetCustomAttributes<LocalizedTextAttributeBase>()
        .Concat(template.GetProperties().SelectMany(p => p.GetCustomAttributes<LocalizedTextAttributeBase>()));

      foreach (var attribute in attributes)
      foreach (var property in attribute.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
      {
        if (property.GetValue(attribute) is not string value)
          continue;

        var offender = FirstCjk(value);
        if (offender is not null)
          offenders.Add($"{template.Name}.{property.Name}: {offender}");
      }
    }

    Assert.True(offenders.Count == 0, $"CJK characters in template hints: {string.Join(", ", offenders)}");
  }

  [Fact]
  public void RulePresetTranslationsCarryNoCjkCharacters()
  {
    var catalog = Path.Combine(FindRepositoryRoot(), "Application", "StockIndicators.Application", "Rules", "Data", "rule-presets.json");

    var offenders = new List<string>();

    foreach (var text in System.Text.Json.JsonDocument.Parse(File.ReadAllText(catalog)).RootElement
               .GetProperty("presets").EnumerateArray()
               .SelectMany(preset => preset.EnumerateObject().SelectMany(member => Texts(member.Value))))
    {
      var offender = FirstCjk(text);
      if (offender is not null)
        offenders.Add($"{offender} in \"{text[..Math.Min(30, text.Length)]}\"");
    }

    Assert.True(offenders.Count == 0, $"CJK characters in the preset catalog: {string.Join(", ", offenders)}");
  }

  // every string the catalog carries: plain properties and the values of the text maps
  private static IEnumerable<string> Texts(System.Text.Json.JsonElement element) =>
    element.ValueKind switch
    {
      System.Text.Json.JsonValueKind.String => [element.GetString()!],
      System.Text.Json.JsonValueKind.Object => element.EnumerateObject().SelectMany(property => Texts(property.Value)),
      _ => [],
    };

  private static string FindRepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "stock-indicators.sln")))
      directory = directory.Parent;

    return directory!.FullName;
  }
}
