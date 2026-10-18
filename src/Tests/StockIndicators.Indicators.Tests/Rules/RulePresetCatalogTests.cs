using Newtonsoft.Json;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules;
using StockIndicators.Application.Rules.GetRulePresets;
using StockIndicators.Contracts.Candles;
using StockIndicators.Contracts.Rules;
using StockIndicators.Infrastructure.Data;
using Xunit;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;

namespace StockIndicators.Indicators.Tests.Rules;

/// <summary>
/// Guards the code-owned rule-preset catalog (Rules/Data/rule-presets.json): every "when"
/// payload must deserialize into the exact RuleGroupDto the rule constructor consumes and pass
/// the same rule validation a user's save goes through, the translations must cover every
/// interface language, and a family chip must stay renderable. A typo in a templateData
/// property name or a non-positive parameter would otherwise only surface at the user's first
/// save - and on the public indicator pages, where the catalog is served anonymously.
/// </summary>
public class RulePresetCatalogTests
{
  private static readonly ISerializer Serializer = new DefaultSerializer(
    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error },
    Log.Logger);

  private static readonly string[] Languages = UserLocale.SupportedLanguages
    .Select(language => language.Code)
    .ToArray();

  [Fact]
  public void Catalog_KeysOrdersAndIndicatorsAreSound()
  {
    var presets = RulePresetCatalog.All;

    Assert.NotEmpty(presets);
    Assert.Equal(presets.Count, presets.Select(preset => preset.Key).Distinct().Count());
    Assert.Equal(presets.Count, presets.Select(preset => preset.Order).Distinct().Count());
    Assert.Equal(presets.Select(preset => preset.Order), presets.Select(preset => preset.Order).OrderBy(order => order));

    // the catalog names the indicator the public page it is shown on belongs to
    Assert.All(presets, preset => Assert.True(
      Enum.TryParse<AppIndicator>(preset.Indicator, out _),
      $"Preset {preset.Key} names the unknown indicator {preset.Indicator}"));
  }

  [Fact]
  public void Catalog_PayloadsDeserializeAndPassSaveValidation()
  {
    foreach (var preset in RulePresetCatalog.All)
    {
      var stored = Serializer.DeserializeOrNull<RuleGroupDto>(preset.When);

      Assert.NotNull(stored);
      // The client fills the instrument placeholders before saving
      Assert.All(stored!.Conditions, condition => Assert.Equal(string.Empty, condition.Ticker));

      // Day is the preset default; normalization must keep the stored echo in sync
      Assert.All(stored.Conditions, condition => Assert.Equal(TimeFrame.Day, condition.TimeFrame));
      RuleValidation.NormalizeConditionTimeFrames(Serializer, stored);
      Assert.All(stored.Conditions, condition => Assert.Equal(TimeFrame.Day, condition.TimeFrame));

      // Every payload has to survive the same validation a user's save goes
      // through, multi-condition combos included
      var group = WithTickers(stored);
      RuleValidation.Validate(group);
      RuleValidation.ValidateConditions(Serializer, group);
    }
  }

  [Fact]
  public void Catalog_SingleConditionPresetValidates()
  {
    // The simplest shape in the catalog (the first preset is one) stays valid
    var single = WithTickers(Serializer.Deserialize<RuleGroupDto>(RulePresetCatalog.All.First().When));

    RuleValidation.Validate(single);
  }

  [Fact]
  public void Catalog_TextsCoverEveryInterfaceLanguage()
  {
    var offenders = new List<string>();

    foreach (var preset in RulePresetCatalog.All)
    {
      Check(preset.Key, "name", preset.Name);
      Check(preset.Key, "description", preset.Description);

      if (preset.Family is null)
      {
        Assert.Null(preset.FamilyLabel);
        Assert.Null(preset.Segment);
        continue;
      }

      Check(preset.Key, "familyLabel", preset.FamilyLabel);
      Check(preset.Key, "segment", preset.Segment);
    }

    Assert.True(offenders.Count == 0, $"Incomplete preset translations: {string.Join(", ", offenders)}");

    void Check(string key, string field, IReadOnlyDictionary<string, string>? texts)
    {
      if (texts is null)
      {
        offenders.Add($"{key}.{field} is missing");
        return;
      }

      foreach (var language in Languages)
      if (string.IsNullOrWhiteSpace(texts.GetValueOrDefault(language)))
        offenders.Add($"{key}.{field} has no {language} text");
    }
  }

  [Fact]
  public void Catalog_FamiliesArePairsWithSharedLabelsAndDistinctSegments()
  {
    foreach (var family in RulePresetCatalog.All.Where(preset => preset.Family is not null).GroupBy(preset => preset.Family))
    {
      // The segmented chip renders two taps side by side: the catalog keeps pairs for now
      Assert.Equal(2, family.Count());

      foreach (var language in Languages)
      {
        Assert.All(family, preset =>
        {
          Assert.False(string.IsNullOrWhiteSpace(preset.FamilyLabel![language]));
          Assert.False(string.IsNullOrWhiteSpace(preset.Segment![language]));
          Assert.True(preset.FamilyLabel[language].Length <= 40, $"{family.Key} label too long in {language}");
          Assert.True(preset.Segment[language].Length <= 40, $"{family.Key} segment too long in {language}");
        });

        // The chip prefix is shared, so the members must agree on it...
        Assert.Single(family.Select(preset => preset.FamilyLabel![language]).Distinct());

        // ...while the segment is what tells the two taps apart
        Assert.Equal(2, family.Select(preset => preset.Segment![language]).Distinct().Count());
      }
    }
  }

  [Fact]
  public async Task Handler_LocalizesEverySupportedLanguage()
  {
    var handler = new GetRulePresetsQueryHandler(Serializer);

    foreach (var language in Languages)
    {
      var presets = await handler.Handle(new GetRulePresetsQuery(language), CancellationToken.None);

      Assert.Equal(RulePresetCatalog.All.Count, presets.Count);
      Assert.All(presets, preset =>
      {
        Assert.False(string.IsNullOrWhiteSpace(preset.Name));
        Assert.False(string.IsNullOrWhiteSpace(preset.Description));

        if (preset.Family is not null)
        {
          Assert.False(string.IsNullOrWhiteSpace(preset.FamilyLabel));
          Assert.False(string.IsNullOrWhiteSpace(preset.Segment));
        }
      });
    }
  }

  [Fact]
  public async Task Handler_UnknownLanguage_AnswersEnglish()
  {
    var handler = new GetRulePresetsQueryHandler(Serializer);

    var localized = await handler.Handle(new GetRulePresetsQuery(UserLocale.English), CancellationToken.None);
    var fallback = await handler.Handle(new GetRulePresetsQuery("zz"), CancellationToken.None);

    Assert.Equal(localized.Select(preset => preset.Name), fallback.Select(preset => preset.Name));
  }

  private static RuleGroupDto WithTickers(RuleGroupDto group) =>
    group with
    {
      Conditions = group.Conditions
        .Select(condition => condition with { Ticker = "SBER", ExchangeName = "MOEX" })
        .ToArray()
    };
}
