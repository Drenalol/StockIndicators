using System.Text.Json;

namespace StockIndicators.Application.Rules;

/// <summary>
/// The built-in one-tap rule presets: ready-made condition trees that pre-fill the rule
/// constructor, with a name, a description and a family-chip label per interface language.
/// <para>
/// The catalog is code-owned rather than seeded into the database: it is product content that
/// changes with a release, never user data, and the public indicator pages serve it - an
/// instance published as the public site runs without a database to read it from.
/// </para>
/// </summary>
public static class RulePresetCatalog
{
  private const string ResourceName = "StockIndicators.Application.Rules.Data.rule-presets.json";

  /// <summary>
  /// One preset. <paramref name="When"/> is the raw RuleGroupDto json (the ticker/exchange
  /// placeholders are empty for the client to fill with the chosen instrument). The text maps
  /// are keyed by interface language; <paramref name="Family"/>, <paramref name="FamilyLabel"/>
  /// and <paramref name="Segment"/> are null for a preset that stands alone.
  /// </summary>
  public sealed record Entry(
    string Key,
    string Indicator,
    int Order,
    string? Family,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyDictionary<string, string>? FamilyLabel,
    IReadOnlyDictionary<string, string>? Segment,
    string When);

  private static readonly Lazy<Entry[]> Loaded = new(Parse);

  /// <summary>The catalog in display order.</summary>
  public static IReadOnlyList<Entry> All => Loaded.Value;

  private static Entry[] Parse()
  {
    using var stream = typeof(RulePresetCatalog).Assembly.GetManifestResourceStream(ResourceName)
      ?? throw new InvalidOperationException($"The rule preset catalog is missing from the assembly: {ResourceName}");

    using var document = JsonDocument.Parse(stream);

    var entries = new List<Entry>();

    foreach (var preset in document.RootElement.GetProperty("presets").EnumerateArray())
    {
      entries.Add(new Entry(
        preset.GetProperty("key").GetString()!,
        preset.GetProperty("indicator").GetString()!,
        preset.GetProperty("order").GetInt32(),
        preset.TryGetProperty("family", out var family) ? family.GetString() : null,
        ReadTexts(preset, "name")!,
        ReadTexts(preset, "description"),
        ReadTexts(preset, "familyLabel"),
        ReadTexts(preset, "segment"),
        // the handler deserializes this with the application serializer, the same way the
        // stored jsonb used to be read: a corrupt fragment must not take the catalog down
        preset.GetProperty("when").GetRawText()));
    }

    return entries.OrderBy(entry => entry.Order).ToArray();
  }

  private static IReadOnlyDictionary<string, string>? ReadTexts(JsonElement preset, string property)
  {
    if (!preset.TryGetProperty(property, out var texts))
      return null;

    return texts.EnumerateObject().ToDictionary(member => member.Name, member => member.Value.GetString()!);
  }
}
