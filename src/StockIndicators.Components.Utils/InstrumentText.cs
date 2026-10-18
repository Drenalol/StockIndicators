using System.Text;

namespace StockIndicators.Components.Utils;

/// <summary>
/// Normalization of instrument ticker/name text for persistent full-text search:
/// the stored forms are lowercase with all non-alphanumeric characters turned into
/// single spaces (names) or removed (tickers), so the search side can normalize the
/// query the same way and compare with cheap operators. Also builds the tsquery
/// fragment: every query token becomes a prefix-lexeme, Cyrillic tokens get a Latin
/// transliteration alternative, tokens are AND-ed.
/// </summary>
public static class InstrumentText
{
  /// <summary>"ПАО «ГАЗПРОМ» ао " -> "п а о газпром ао" style: lowercase, punctuation to spaces.</summary>
  public static string NormalizeName(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return string.Empty;

    var builder = new StringBuilder(name.Length + 8);
    var pendingSpace = false;

    foreach (var ch in name.ToLowerInvariant())
    {
      if (char.IsLetterOrDigit(ch))
      {
        if (pendingSpace && builder.Length > 0)
          builder.Append(' ');

        builder.Append(ch);
        pendingSpace = false;
      }
      else
        pendingSpace = builder.Length > 0;
    }

    return builder.ToString();
  }

  /// <summary>"BTC/USDT" -> "btcusdt": lowercase without separators.</summary>
  public static string NormalizeTicker(string ticker)
  {
    var builder = new StringBuilder(ticker.Length);

    foreach (var ch in ticker.ToLowerInvariant())
      if (char.IsLetterOrDigit(ch))
        builder.Append(ch);

    return builder.ToString();
  }

  /// <summary>
  /// Builds a Postgres tsquery for the 'simple' configuration from a raw query: every word
  /// token becomes a prefix-lexeme, tokens are AND-ed; a Cyrillic token becomes an OR of its
  /// own, romanized, k-to-c phonetic ("биткоин" - "bitkoin" - "bitcoin") and dictionary
  /// ("эфир" - "ethereum") lexemes, so one query matches the Cyrillic name, its transliteration
  /// and the canonical Latin crypto spelling. tsquery operators and punctuation are stripped
  /// from tokens. Returns null when no word characters remain.
  /// </summary>
  public static string? BuildTsQuery(string? query)
  {
    if (string.IsNullOrWhiteSpace(query))
      return null;

    var parts = new List<string>();

    foreach (var token in SplitTokens(query))
    {
      var lexeme = SanitizeLexeme(token);

      if (lexeme.Length == 0)
        continue;

      var variants = new List<string> { lexeme };

      foreach (var variant in LatinVariants(lexeme))
        if (!variants.Contains(variant))
          variants.Add(variant);

      parts.Add(variants.Count == 1
        ? $"{lexeme}:*"
        : "(" + string.Join(" | ", variants.Select(v => v + ":*")) + ")");
    }

    return parts.Count == 0 ? null : string.Join(" & ", parts.Distinct());
  }

  /// <summary>
  /// Latin spellings a Cyrillic token can stand for: the plain romanization, its k-to-c
  /// phonetic variant (Cyrillic "к" writes both Latin k and c: "биткоин"/"bitcoin",
  /// "догикоин"/"dogecoin"), and the curated crypto-name dictionary ("эфир" - "ethereum").
  /// </summary>
  public static IReadOnlyList<string> LatinVariants(string token)
  {
    var variants = new List<string>();

    var translit = SanitizeLexeme(Transliteration.RuToLatin(token) ?? string.Empty);

    if (translit.Length > 0 && translit != token)
      variants.Add(translit);

    // the k-to-c variant is only meaningful for a transliterated Cyrillic token
    // (a Latin "bank" must not grow a phantom "banc")
    if (translit.Length > 0)
    {
      var phonetic = translit.Replace('k', 'c');

      if (phonetic.Length > 0 && phonetic != token && !variants.Contains(phonetic))
        variants.Add(phonetic);
    }

    var dictionary = CryptoCyrillicNames.Translate(token);

    if (dictionary is not null && dictionary != token && !variants.Contains(dictionary))
      variants.Add(dictionary);

    return variants;
  }

  /// <summary>Splits a raw search query into word tokens on punctuation and tsquery operators.</summary>
  public static IReadOnlyList<string> SplitTokens(string query) =>
    query.Split([' ', '-', '_', '/', '.', ',', ':', ';', '\'', '"', '(', ')', '[', ']', '&', '|', '!', '<', '>', '~', '*', '?', '{', '}', '\\', '@', '+', '=', '%', '$', '#', '^', '`'], StringSplitOptions.RemoveEmptyEntries);

  private static string SanitizeLexeme(string token)
  {
    var builder = new StringBuilder(token.Length);

    foreach (var ch in token.ToLowerInvariant())
      if (char.IsLetterOrDigit(ch))
        builder.Append(ch);

    return builder.ToString();
  }
}

/// <summary>
/// Extracts the base asset symbol of a crypto instrument ("BTC/USDT" name or "BTCUSDT"
/// ticker -> "btc"): the base is what CoinGecko-style asset name catalogs are keyed by.
/// </summary>
public static class CryptoBaseAsset
{
  // Quote assets the providers list crypto pairs against (Binance filter + Alpaca).
  private static readonly string[] QuoteSuffixes =
  [
    "USDT", "USDC", "USD", "USDD", "FDUSD", "TUSD", "BUSD", "DAI", "EUR", "TRY", "RUB",
    "BTC", "ETH", "BNB", "XMR",
  ];

  public static string Extract(string? name, string ticker, string? quoteCurrency)
  {
    // Provider names are "BASE/QUOTE" for crypto (Binance "BTC/USDT", Alpaca "BTC/USD")
    if (name is not null)
    {
      var slash = name.IndexOf('/');

      if (slash > 0)
        return name[..slash].ToLowerInvariant();
    }

    var upper = ticker.ToUpperInvariant();

    if (quoteCurrency is { Length: > 0 } && upper.EndsWith(quoteCurrency.ToUpperInvariant(), StringComparison.Ordinal) &&
        upper.Length > quoteCurrency.Length)
      return upper[..^quoteCurrency.Length].ToLowerInvariant();

    foreach (var quote in QuoteSuffixes)
      if (upper.EndsWith(quote, StringComparison.Ordinal) && upper.Length > quote.Length)
        return upper[..^quote.Length].ToLowerInvariant();

    return upper.ToLowerInvariant();
  }
}
