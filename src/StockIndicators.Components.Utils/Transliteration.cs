using System.Text;

namespace StockIndicators.Components.Utils;

/// <summary>
/// Cyrillic-to-Latin romanization for instrument search: MOEX instruments carry Russian
/// names ("Газпром"), so a Latin query ("gazprom") is transliterated before it is matched
/// against the stored name. The mapping is the practical BGN/PCGN-style one used for
/// search purposes: digraphs (zh, ts, ch, sh, shch, yu, ya, yo, y) rather than scientific
/// transliteration.
/// </summary>
public static class Transliteration
{
  private static readonly Dictionary<char, string> Map = new()
  {
    ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo",
    ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
    ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
    ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch",
    ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
    // Ukrainian and Belarusian extras (uk locale users search MOEX names too)
    ['є'] = "e", ['і'] = "i", ['ї'] = "yi", ['ґ'] = "g", ['ў'] = "u",
  };

  /// <summary>
  /// Romanizes Cyrillic characters; every other character passes through unchanged.
  /// Returns null when the input has no Cyrillic letters (nothing to gain from a variant).
  /// </summary>
  public static string? RuToLatin(string? text)
  {
    if (string.IsNullOrEmpty(text))
      return null;

    var any = false;
    var builder = new StringBuilder(text.Length * 2);

    foreach (var ch in text.ToLowerInvariant())
    {
      if (Map.TryGetValue(ch, out var replacement))
      {
        builder.Append(replacement);
        any = true;
      }
      else
        builder.Append(ch);
    }

    return any ? builder.ToString() : null;
  }
}
