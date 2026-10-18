using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Rules.GetRulePresets;

/// <summary>
/// The built-in rule-preset catalog localized into <paramref name="Language"/> (any interface
/// language, English for one the catalog does not carry): one-tap condition trees that pre-fill
/// the rule constructor.
/// </summary>
public record GetRulePresetsQuery(string Language) : IQuery<IReadOnlyList<RulePresetDto>>;

/// <summary>
/// A preset as the rule constructor consumes it: <paramref name="When"/> is a ready
/// RuleGroupDto fragment whose ticker/exchange placeholders the client fills with the
/// selected instrument.
/// Presets sharing a <paramref name="Family"/> render as segments of one chip:
/// <paramref name="FamilyLabel"/> is the shared chip prefix, <paramref name="Segment"/>
/// the button label of this preset inside the chip (both null for standalone presets).
/// </summary>
public record RulePresetDto(
  string Key,
  string Indicator,
  string Name,
  string? Description,
  RuleGroupDto When,
  string? Family = null,
  string? FamilyLabel = null,
  string? Segment = null);
