using System.Data;
using Dapper;
using StockIndicators.Components.Utils;

namespace StockIndicators.Application.Instruments;

/// <summary>
/// Prepared parameters and SQL fragments for searching the persistent "Instruments" table
/// by free text. A Cyrillic query expands to several Latin forms (transliteration, its
/// k-to-c phonetic variant, the curated crypto-name dictionary), so one search matches the
/// Russian MOEX name and the canonical crypto spelling alike. The ranking ORDER BY uses
/// the match tiers: exact ticker, ticker prefix, exact alias of a top-market-cap coin,
/// exact alias of any coin, name word prefix, full-text prefix hit and finally trigram
/// similarity (typo fallback, e.g. "aple" - AAPL). Shared with the subscription list
/// filter, which EXISTS-joins instruments.
/// </summary>
public sealed class InstrumentSearchParameters
{
  private const int MaxQueryLength = 64;

  /// <summary>Normalized query forms: the raw query first, then its Latin variants.</summary>
  public string[] Forms { get; }

  /// <summary>LIKE-escaped raw query (for free-text columns outside the instruments table, e.g. rule names).</summary>
  public string QLike { get; }

  private string[] PrefixForms { get; }
  private string[] MidForms { get; }
  private string[] WordPrefixForms { get; }
  public string? TsQuery { get; }

  private InstrumentSearchParameters(string[] forms, string? tsQuery)
  {
    Forms = forms;
    TsQuery = tsQuery;

    var escaped = forms.Select(EscapeLike).ToArray();

    QLike = escaped[0];

    PrefixForms = escaped.Select(f => f + "%").ToArray();
    MidForms = escaped.Select(f => "%" + f + "%").ToArray();
    WordPrefixForms = escaped.Select(f => "% " + f + "%").ToArray();
  }

  /// <summary>
  /// Normalizes the raw query; returns null when nothing searchable is left (punctuation only).
  /// </summary>
  public static InstrumentSearchParameters? Build(string? search)
  {
    if (string.IsNullOrWhiteSpace(search))
      return null;

    var q = search.Trim();

    if (q.Length > MaxQueryLength)
      q = q[..MaxQueryLength];

    q = InstrumentText.NormalizeName(q);

    if (q.Length == 0)
      return null;

    var forms = new List<string> { q };

    var translit = Transliteration.RuToLatin(q);

    if (translit is not null)
    {
      forms.Add(translit);

      // Cyrillic "к" stands for both Latin k and c: "биткоин" romanizes to "bitkoin",
      // the canonical spelling is "bitcoin"
      var phonetic = translit.Replace('k', 'c');

      if (phonetic != q)
        forms.Add(phonetic);
    }

    var dictionary = CryptoCyrillicNames.TranslateQuery(q);

    if (dictionary is not null)
      forms.Add(dictionary);

    return new InstrumentSearchParameters(forms.Distinct().ToArray(), InstrumentText.BuildTsQuery(q));
  }

  public void AddTo(DynamicParameters parameters)
  {
    parameters.Add("Forms", Forms);
    parameters.Add("QLike", QLike, DbType.String);
    parameters.Add("Pfx", PrefixForms);
    parameters.Add("Mid", MidForms);
    parameters.Add("WordPfx", WordPrefixForms);
    parameters.Add("TsQuery", (object?)TsQuery ?? DBNull.Value, DbType.String);
  }

  /// <summary>Match predicate over an "Instruments" alias.</summary>
  public string Where(string i) =>
    $"""
      (
        {i}."TickerNorm" = ANY(@Forms)
        OR {i}."TickerNorm" LIKE ANY(@Pfx)
        OR {i}."NameNorm" LIKE ANY(@Mid)
        OR {i}."NameTranslit" LIKE ANY(@Mid)
        OR (@TsQuery IS NOT NULL AND {i}."Search" @@ to_tsquery('simple', @TsQuery))
        OR {i}."TickerNorm" % ANY(@Forms)
        OR {i}."NameNorm" % ANY(@Forms)
      )
      """;

  /// <summary>
  /// Match tier of the predicate above: 0 exact ticker, 1 ticker prefix, 2 exact alias of a
  /// top-market-cap coin (the canonical "bitcoin"), 3 exact alias of any coin sharing the
  /// symbol (a clone), 4 name word prefix, 5 full-text prefix hit, 6 trigram similarity only.
  /// </summary>
  public string Rank(string i) =>
    $"""
      CASE
        WHEN {i}."TickerNorm" = ANY(@Forms) THEN 0
        WHEN {i}."TickerNorm" LIKE ANY(@Pfx) THEN 1
        WHEN @Forms && {i}."AliasesTop" THEN 2
        WHEN @Forms && {i}."Aliases" THEN 3
        WHEN {i}."NameNorm" LIKE ANY(@Pfx) OR {i}."NameNorm" LIKE ANY(@WordPfx)
             OR {i}."NameTranslit" LIKE ANY(@Pfx) OR {i}."NameTranslit" LIKE ANY(@WordPfx) THEN 4
        WHEN @TsQuery IS NOT NULL AND {i}."Search" @@ to_tsquery('simple', @TsQuery) THEN 5
        ELSE 6
      END
      """;

  private static string EscapeLike(string value) =>
    value.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("%", "\\%", StringComparison.Ordinal)
         .Replace("_", "\\_", StringComparison.Ordinal);
}
