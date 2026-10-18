using System.Globalization;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Indicators.Week52HighLow;

namespace StockIndicators.Application.Configuration.Locale;

/// <summary>
/// Thin accessors over <see cref="LocalizationCatalog"/> for alert text. Numbers and
/// indicator names stay in invariant technical form; only the words around them are
/// localized, and the words themselves live in the catalog — not in per-language switches.
/// </summary>
public static class AlertTexts
{
  public static string RuleHeader(string language, bool met) =>
    LocalizationCatalog.Get(met ? LocalizationKey.RuleHeaderMet : LocalizationKey.RuleHeaderNotMet, language);

  public static string Triggered(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Triggered, language);

  public static string ConditionMet(string language) =>
    LocalizationCatalog.Get(LocalizationKey.ConditionMet, language);

  public static string GapPreviousLabel(string language) =>
    LocalizationCatalog.Get(LocalizationKey.GapPreviousLabel, language);

  public static string MaTestLabel(string language, MaTest test) =>
    LocalizationCatalog.Get(
      test switch
      {
        MaTest.Upward => LocalizationKey.MaTestUpward,
        MaTest.Downward => LocalizationKey.MaTestDownward,
        _ => LocalizationKey.MaTestNone
      },
      language);

  public static string VolumeAverageLabel(string language) =>
    LocalizationCatalog.Get(LocalizationKey.VolumeAverageLabel, language);

  public static string OpenChangeLabel(string language) =>
    LocalizationCatalog.Get(LocalizationKey.OpenChangeLabel, language);

  public static string Week52Label(string language, Week52AlertKind kind) =>
    LocalizationCatalog.Get(
      kind switch
      {
        Week52AlertKind.NewHigh => LocalizationKey.Week52AboveHigh,
        Week52AlertKind.NewLow => LocalizationKey.Week52BelowLow,
        Week52AlertKind.PullbackFromHigh => LocalizationKey.Week52PullbackHigh,
        Week52AlertKind.BounceFromLow => LocalizationKey.Week52BounceLow,
        _ => LocalizationKey.Triggered,
      },
      language);

  public static string TrailingAnchorLabel(string language, bool longPosition) =>
    LocalizationCatalog.Get(longPosition ? LocalizationKey.TrailingFromMax : LocalizationKey.TrailingFromMin, language);

  public static string TelegramHeader(string language) =>
    LocalizationCatalog.Get(LocalizationKey.TelegramHeader, language);

  public static string TelegramOpenButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.TelegramOpenButton, language);

  public static string FormatNumber(double value, string format) =>
    value.ToString(format, CultureInfo.InvariantCulture);

  public static string FormatDate(DateTime utcDate, TimeZoneInfo timezone, string format) =>
    TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDate, DateTimeKind.Utc), timezone).ToString(format);
}
