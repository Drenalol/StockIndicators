using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Application.Rules.GetRulePresets;

public class GetRulePresetsQueryHandler : IQueryHandler<GetRulePresetsQuery, IReadOnlyList<RulePresetDto>>
{
  private readonly ISerializer _serializer;

  public GetRulePresetsQueryHandler(ISerializer serializer) => _serializer = serializer;

  public Task<IReadOnlyList<RulePresetDto>> Handle(GetRulePresetsQuery request, CancellationToken cancellationToken)
  {
    var presets = RulePresetCatalog.All;

    var result = new List<RulePresetDto>(presets.Count);

    foreach (var preset in presets)
    {
      // A corrupt payload is skipped rather than failing the whole catalog
      if (_serializer.DeserializeOrNull<RuleGroupDto>(preset.When) is not { } when)
        continue;

      result.Add(new RulePresetDto(
        preset.Key,
        preset.Indicator,
        Localize(preset.Name, request.Language)!,
        Localize(preset.Description, request.Language),
        when,
        preset.Family,
        Localize(preset.FamilyLabel, request.Language),
        Localize(preset.Segment, request.Language)));
    }

    return Task.FromResult<IReadOnlyList<RulePresetDto>>(result);
  }

  // A language the catalog carries no text for answers English: a preset is never shown
  // without a label. Family labels and segments are null for a preset that stands alone.
  private static string? Localize(IReadOnlyDictionary<string, string>? texts, string language) =>
    texts is null ? null : texts.GetValueOrDefault(language) ?? texts.GetValueOrDefault("en");
}
