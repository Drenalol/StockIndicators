using System.Text.RegularExpressions;

namespace StockIndicators.Components.Utils;

/// <summary>
/// Best-effort instrument category inference from a display name.
/// Only strong, unambiguous markers are used; unknown names yield null.
/// </summary>
public static class InstrumentTypeInference
{
  private static readonly Regex EtfWordRegex = new(@"\b(ETF|ETP)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

  // Word-boundary on purpose: "фондовый" (adjective) must not match.
  private static readonly Regex RuFundWordRegex = new(@"\bфонд\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

  private static readonly string[] EtfMarkers = ["бестуб", "бпиф"];
  private static readonly string[] BondMarkers = ["облигац"];
  private static readonly string[] OptionMarkers = ["опцион"];
  private static readonly string[] FutureMarkers = ["фьючер"];

  public static string? Infer(string? name, string? longName = null)
  {
    var text = string.Join(' ', name ?? string.Empty, longName ?? string.Empty);
    if (string.IsNullOrWhiteSpace(text))
      return null;

    // Funds take precedence over their underlying: "iShares ... Bond ETF" is an Etf,
    // "Фонд ... облигаций" is an Etf, not a Bond.
    if (EtfWordRegex.IsMatch(text) || RuFundWordRegex.IsMatch(text) || ContainsAny(text, EtfMarkers))
      return "Etf";
    if (ContainsAny(text, BondMarkers))
      return "Bond";
    if (ContainsAny(text, OptionMarkers))
      return "Option";
    if (ContainsAny(text, FutureMarkers))
      return "Future";
    return null;

    static bool ContainsAny(string text, string[] markers) =>
      markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
  }
}
