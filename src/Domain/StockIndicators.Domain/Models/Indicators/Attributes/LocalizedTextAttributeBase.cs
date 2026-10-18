namespace StockIndicators.Domain.Models.Indicators.Attributes;

/// <summary>
/// Base for localized hint attributes: one named string property per language.
/// Properties stay null when a language is not translated, and the reader falls back
/// to <see cref="FallbackLanguage"/> (English by default). New languages are added as
/// optional properties, so existing attribute usages never break.
/// </summary>
public abstract class LocalizedTextAttributeBase : Attribute
{
  public string? En { get; set; }

  public string? Ru { get; set; }

  public string? Es { get; set; }

  public string? It { get; set; }

  public string? De { get; set; }

  public string? Fr { get; set; }

  /// <summary>Brazilian Portuguese, language code "pt-BR".</summary>
  public string? PtBr { get; set; }

  public string? Uk { get; set; }

  public string? Be { get; set; }

  /// <summary>
  /// Language code used when the requested language has no value. Defaults to "en".
  /// </summary>
  public string FallbackLanguage { get; set; } = "en";

  protected LocalizedTextAttributeBase()
  {
  }

  protected LocalizedTextAttributeBase(string en, string ru)
  {
    En = en;
    Ru = ru;
  }

  /// <summary>
  /// The text for <paramref name="language"/> (a language code like "ru"), falling back
  /// to <see cref="FallbackLanguage"/> when this language is not translated.
  /// </summary>
  public string? GetText(string? language)
  {
    var code = language?.ToLowerInvariant();

    var text = code switch
    {
      "ru" => Ru,
      "es" => Es,
      "it" => It,
      "de" => De,
      "fr" => Fr,
      "pt-br" => PtBr,
      "uk" => Uk,
      "be" => Be,
      "en" => En,
      _ => null
    };

    if (!string.IsNullOrWhiteSpace(text))
      return text;

    return FallbackLanguage.ToLowerInvariant() switch
    {
      "ru" => Ru,
      "es" => Es,
      "it" => It,
      "de" => De,
      "fr" => Fr,
      "pt-br" => PtBr,
      "uk" => Uk,
      "be" => Be,
      _ => En
    };
  }
}
