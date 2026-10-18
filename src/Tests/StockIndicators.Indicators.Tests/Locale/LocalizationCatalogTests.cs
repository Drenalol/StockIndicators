using StockIndicators.Application.Configuration.Locale;
using Xunit;

namespace StockIndicators.Indicators.Tests.Locale;

/// <summary>
/// Guards the localization infrastructure: every catalog key must be translated into
/// every registered language, and the language registry must stay consistent.
/// Adding a language or a key without translations fails here instead of at runtime.
/// </summary>
public class LocalizationCatalogTests
{
  [Fact]
  public void EveryKeyHasAValueInEverySupportedLanguage()
  {
    var missing = new List<string>();

    foreach (var key in Enum.GetValues<LocalizationKey>())
    foreach (var descriptor in UserLocale.SupportedLanguages)
    {
      var value = LocalizationCatalog.Get(key, descriptor.Code);

      // Get falls back to the default language, so a missing translation for a
      // non-default language equals the English value — flag those explicitly.
      if (descriptor.Code != UserLocale.DefaultLanguage &&
          string.Equals(value, LocalizationCatalog.Get(key, UserLocale.DefaultLanguage), StringComparison.Ordinal) &&
          !AllowedSharedValue(key, descriptor.Code))
        missing.Add($"{key}/{descriptor.Code}");
    }

    Assert.True(missing.Count == 0, $"Keys without a translation in their language: {string.Join(", ", missing)}");
  }

  [Fact]
  public void EveryKeyHasNonEmptyValue()
  {
    foreach (var key in Enum.GetValues<LocalizationKey>())
      Assert.False(string.IsNullOrWhiteSpace(LocalizationCatalog.Get(key, UserLocale.DefaultLanguage)), $"Key '{key}' has no English value.");
  }

  [Fact]
  public void UnknownLanguageFallsBackToDefaultLanguage()
  {
    Assert.Equal(
      LocalizationCatalog.Get(LocalizationKey.Back, UserLocale.DefaultLanguage),
      LocalizationCatalog.Get(LocalizationKey.Back, "zz"));
  }

  [Fact]
  public void FormattedPlaceholdersAreReplaced()
  {
    var text = LocalizationCatalog.GetFormatted(LocalizationKey.TimeZoneInvalid, UserLocale.English, ("value", "Mars/Olympus_Mons"));

    Assert.Contains("Mars/Olympus_Mons", text);
    Assert.DoesNotContain("{value}", text);
  }

  [Fact]
  public void RegistryMatchesLanguageConstants()
  {
    var codes = UserLocale.SupportedLanguages.Select(descriptor => descriptor.Code).ToList();

    Assert.Contains(UserLocale.Russian, codes);
    Assert.Contains(UserLocale.English, codes);
    Assert.Contains(UserLocale.Spanish, codes);
    Assert.Contains(UserLocale.Italian, codes);
    Assert.Contains(UserLocale.German, codes);
    Assert.Contains(UserLocale.French, codes);
    Assert.Contains(UserLocale.PortugueseBrazil, codes);
    Assert.Contains(UserLocale.Ukrainian, codes);
    Assert.Contains(UserLocale.Belarusian, codes);
    Assert.Contains(UserLocale.DefaultLanguage, codes);
  }

  /// <summary>
  /// Values that are legitimately identical across languages (indicator names, units).
  /// </summary>
  private static bool AllowedSharedValue(LocalizationKey key, string language) =>
    key is LocalizationKey.IntervalUnitHour
      or LocalizationKey.IntervalUnitSecond
      or LocalizationKey.IntervalUnitMinute
      or LocalizationKey.IntervalUnitDay
      or LocalizationKey.InstrumentFieldSector
      // the English language's own display name is "English" everywhere
      or LocalizationKey.LanguageNameEn
      // "Vol" reads the same in every language (the web legend shows it too)
      or LocalizationKey.ChartLegendVolume
      or LocalizationKey.No
      // the linked-chat line is a bullet + chat label — pure punctuation around it
      or LocalizationKey.ChatLine
      // the method-of-sign-in rows keep the brand names ("via Google" reads alike)
      or LocalizationKey.NewDeviceMethodGoogle
      or LocalizationKey.NewDeviceMethodTelegram
      // true cognates: the word is spelled identically in that language
      || (key == LocalizationKey.Instruments && language == UserLocale.French)
      || (key == LocalizationKey.InstrumentFieldName && language == UserLocale.German)
      || (key == LocalizationKey.InstrumentFieldType && language == UserLocale.French)
      || (key == LocalizationKey.SubscriptionFieldInstrument
          && language is UserLocale.German or UserLocale.French)
      || (key == LocalizationKey.SubscriptionFieldStatus && language == UserLocale.German);
}
