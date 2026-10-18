namespace StockIndicators.Components.Utils;

/// <summary>
/// Curated Cyrillic spellings of crypto asset names that mechanical transliteration
/// cannot produce: "биткоин" romanizes to "bitkoin" (not "bitcoin", Latin c is Cyrillic
/// "ц") and "эфир" to "efir" (not "ethereum"). The map is token-level, so compound names
/// translate word by word ("биткоин кеш" - "bitcoin cash"). Keep it to names users
/// actually type in Russian; unknown tokens pass through.
/// </summary>
public static class CryptoCyrillicNames
{
  private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
  {
    ["биткоин"] = "bitcoin",
    ["биткойн"] = "bitcoin",
    ["кеш"] = "cash",
    ["кэш"] = "cash",
    ["эфир"] = "ethereum",
    ["эфириум"] = "ethereum",
    ["ефириум"] = "ethereum",
    ["догикоин"] = "dogecoin",
    ["доги"] = "doge",
    ["лайткоин"] = "litecoin",
    ["литкоин"] = "litecoin",
    ["кардано"] = "cardano",
    ["монеро"] = "monero",
    ["рипл"] = "ripple",
    ["тезер"] = "tether",
    ["биткоинкеш"] = "bitcoincash",
  };

  /// <summary>Russian spelling of a crypto asset name, or null when the token is not known.</summary>
  public static string? Translate(string token) => Map.TryGetValue(token, out var name) ? name : null;

  /// <summary>
  /// Translates every word of a (normalized, lowercase) query; null when no word is known
  /// or the result equals the input.
  /// </summary>
  public static string? TranslateQuery(string query)
  {
    if (string.IsNullOrWhiteSpace(query))
      return null;

    var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var any = false;
    var translated = new string[tokens.Length];

    for (var i = 0; i < tokens.Length; i++)
    {
      translated[i] = Map.TryGetValue(tokens[i], out var name) ? name : tokens[i];
      any |= translated[i] != tokens[i];
    }

    if (!any)
      return null;

    return string.Join(' ', translated);
  }
}
