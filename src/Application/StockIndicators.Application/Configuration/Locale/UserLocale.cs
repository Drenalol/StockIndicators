namespace StockIndicators.Application.Configuration.Locale;

/// <summary>
/// A user's notification locale: the language alert texts are rendered in and the timezone
/// dates on trigger charts and in alert templates are drawn in. The chart display options
/// ride along so the alert image applies the same tooltip option the app shows (the chart
/// preferences document is owned by <c>ChartPreferencesDto</c>; only options meaningful to
/// the static image are surfaced here).
/// </summary>
public sealed record UserLocale(string Language, TimeZoneInfo Timezone, bool ShowOhlcTooltip = true)
{
  public const string Russian = "ru";
  public const string English = "en";
  public const string Spanish = "es";
  public const string Italian = "it";
  public const string German = "de";
  public const string French = "fr";
  public const string PortugueseBrazil = "pt-BR";
  public const string Ukrainian = "uk";
  public const string Belarusian = "be";
  public const string DefaultLanguage = English;

  /// <summary>
  /// The single registry of supported languages. Adding a language means adding a code
  /// constant and one entry here; every picker, validator and fallback reads this list.
  /// Codes double as frontend route locales, so they must exist in SUPPORTED_LOCALES there.
  /// </summary>
  public static IReadOnlyList<LanguageDescriptor> SupportedLanguages { get; } =
  [
    new(Russian, "Русский"),
    new(English, "English"),
    new(Spanish, "Español"),
    new(Italian, "Italiano"),
    new(German, "Deutsch"),
    new(French, "Français"),
    new(PortugueseBrazil, "Português (Brasil)"),
    new(Ukrainian, "Українська"),
    new(Belarusian, "Беларуская")
  ];

  public static readonly UserLocale Default = new(DefaultLanguage, TimeZoneInfo.Utc);

  public static bool IsSupportedLanguage(string? language) =>
    SupportedLanguages.Any(descriptor => string.Equals(descriptor.Code, language, StringComparison.OrdinalIgnoreCase));

  public static string NormalizeLanguage(string? language) =>
    SupportedLanguages.FirstOrDefault(descriptor => string.Equals(descriptor.Code, language, StringComparison.OrdinalIgnoreCase))?.Code
    ?? DefaultLanguage;

  public static string NativeName(string language) =>
    SupportedLanguages.FirstOrDefault(descriptor => string.Equals(descriptor.Code, language, StringComparison.OrdinalIgnoreCase))?.NativeName
    ?? language;
}

/// <summary>
/// A supported language: its code plus the name shown to the user in their own language
/// (used by language pickers; the localized-in-current-language name lives in the catalog).
/// </summary>
public sealed record LanguageDescriptor(string Code, string NativeName);
