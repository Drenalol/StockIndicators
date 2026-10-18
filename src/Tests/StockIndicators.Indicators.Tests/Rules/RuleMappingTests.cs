using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Rules;
using StockIndicators.Contracts.Candles;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

public class RuleMappingTests
{
  private static RuleGroupDto CreateGroupDto() => new(
    Guid.NewGuid(),
    LogicalOperatorDto.And,
    [
      new RuleConditionDto(
        Guid.NewGuid(),
        "SMA",
        JObject.Parse("""{"Periods": 20, "TimeFrame": 0}"""),
        ConditionOperatorDto.GreaterThan,
        ReferenceTypeDto.Value,
        null,
        null,
        null,
        null,
        0.0005m,
        "SBER",
        "MOEX",
        TimeFrame.Minute
      ),
      new RuleConditionDto(
        Guid.NewGuid(),
        "StochRSI",
        JObject.Parse("""{"TimeFrame": 0}"""),
        ConditionOperatorDto.LessThan,
        ReferenceTypeDto.Value,
        null,
        null,
        null,
        null,
        null,
        "SBER",
        "MOEX",
        TimeFrame.Minute
      )
    ],
    []
  );

  [Fact]
  public void DtoToDomain_PreservesTree()
  {
    var ruleId = Guid.NewGuid();
    var domain = RuleMappers.ToDomain(ruleId, "Test", CreateGroupDto(), null, null);

    Assert.Equal(ruleId, domain.Id);
    Assert.Equal("Test", domain.Name);
    Assert.Equal(ruleId, domain.When.Id);
    Assert.Equal(LogicalOperator.And, domain.When.LogicalOperator);
    Assert.Equal(2, domain.When.Conditions.Count);
    Assert.Equal("SMA", domain.When.Conditions[0].Indicator.ToString());
    Assert.Equal("""{"Periods":20,"TimeFrame":0}""", domain.When.Conditions[0].TemplateData);
    Assert.Equal(0.0005m, domain.When.Conditions[0].Deviation);
    Assert.Equal("SBER", domain.When.Conditions[0].Ticker);
    Assert.Equal(Domain.Models.Candles.TimeFrame.Minute, domain.When.Conditions[0].TimeFrame);
  }

  [Fact]
  public void DomainToDto_RoundTrip()
  {
    var ruleId = Guid.NewGuid();
    var domain = RuleMappers.ToDomain(ruleId, "Test", CreateGroupDto(), null, null);
    var dto = RuleMappers.ToDto(domain);

    Assert.Equal(ruleId, dto.Id);
    Assert.Equal(LogicalOperatorDto.And, dto.When.Operator);
    Assert.Equal(2, dto.When.Conditions.Length);
    Assert.Equal("SMA", dto.When.Conditions[0].Indicator);
    Assert.Equal(20, (int)dto.When.Conditions[0].TemplateData!["Periods"]!);
    Assert.Equal(0.0005m, dto.When.Conditions[0].Deviation);
    Assert.Equal(TimeFrame.Minute, dto.When.Conditions[0].TimeFrame);
  }

  [Fact]
  public void Validation_EmptyGroup_Throws()
  {
    Assert.Throws<ValidationException>(() => RuleValidation.Validate(new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.And, [], [])));
  }

  [Fact]
  public void Validation_NestedEmptyGroup_Throws()
  {
    var group = new RuleGroupDto(
      Guid.NewGuid(),
      LogicalOperatorDto.And,
      [],
      [new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.Or, [], [])]
    );

    Assert.Throws<ValidationException>(() => RuleValidation.Validate(group));
  }

  [Fact]
  public void Validation_ValidDeepGroup_Passes()
  {
    // A two-condition tree fits both plans' ceilings (free: 5, premium: 20)
    RuleValidation.Validate(CreateGroupDto());
  }

  [Fact]
  public void Validation_ConditionWithoutTicker_Throws()
  {
    var group = new RuleGroupDto(
      Guid.NewGuid(),
      LogicalOperatorDto.And,
      [
        new RuleConditionDto(
          Guid.NewGuid(), "SMA", null,
          ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value,
          null, null, null, null, null, "", null, TimeFrame.Minute)
      ],
      []
    );

    Assert.Throws<ValidationException>(() => RuleValidation.Validate(group));
  }

  private static RuleActionDto Action(ActionTypeDto type) => new(null, type, null, null);

  [Fact]
  public void ValidateActions_DistinctTypesPerBranch_Passes()
  {
    RuleValidation.ValidateActions(
      [Action(ActionTypeDto.SendNotification), Action(ActionTypeDto.DisableSubscription)],
      [Action(ActionTypeDto.SendNotification)]
    );
  }

  [Fact]
  public void ValidateActions_EmptyBranches_Passes()
  {
    RuleValidation.ValidateActions(null, null);
    RuleValidation.ValidateActions([], []);
  }

  [Fact]
  public void ValidateActions_DuplicateTypeInBranch_Throws()
  {
    Assert.Throws<ValidationException>(() => RuleValidation.ValidateActions(
      [Action(ActionTypeDto.SendNotification), Action(ActionTypeDto.SendNotification)],
      null
    ));
  }

  [Fact]
  public void ValidateActions_TooManyActions_Throws()
  {
    Assert.Throws<ValidationException>(() => RuleValidation.ValidateActions(
      [Action(ActionTypeDto.SendNotification), Action(ActionTypeDto.DisableSubscription), Action(ActionTypeDto.DisableSubscription)],
      null
    ));
  }

  [Fact]
  public void ValidateName_Valid_Passes()
  {
    RuleValidation.ValidateName("SMA & EMA (MSFT)");
    RuleValidation.ValidateName(new string('a', RuleValidation.MaxRuleNameLength));
  }

  [Fact]
  public void ValidateName_TooLong_Throws()
  {
    Assert.Throws<ValidationException>(() => RuleValidation.ValidateName(new string('a', RuleValidation.MaxRuleNameLength + 1)));
  }

  [Fact]
  public void ValidateName_Empty_Throws()
  {
    Assert.Throws<ValidationException>(() => RuleValidation.ValidateName("   "));
    Assert.Throws<ValidationException>(() => RuleValidation.ValidateName(null));
  }

  private static RuleGroupDto LeafGroup() => new(
    Guid.NewGuid(), LogicalOperatorDto.And,
    [new RuleConditionDto(Guid.NewGuid(), "SMA", null, ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value, null, null, null, null, null, "SBER", "MOEX", TimeFrame.Minute)],
    []);

  [Fact]
  public void Validate_NestingThreeLevels_Passes()
  {
    var level3 = LeafGroup();
    var level2 = new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.And, [], [level3]);
    var root = new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.And, [], [level2]);

    RuleValidation.Validate(root);
  }

  [Fact]
  public void Validate_ThreeConditionsAcrossGroups_Passes()
  {
    var root = new RuleGroupDto(
      Guid.NewGuid(),
      LogicalOperatorDto.And,
      [Condition("SMA", null), Condition("EMA", null)],
      [new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.Or, [Condition("RSI", null)], [])]
    );

    RuleValidation.Validate(root);
  }

  private static ISerializer Serializer => new DefaultSerializer(
    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error },
    Log.Logger);

  private static RuleConditionDto Condition(string indicator, string? templateJson, string ticker = "SBER") =>
    new(
      Guid.NewGuid(),
      indicator,
      templateJson is null ? null : JObject.Parse(templateJson),
      ConditionOperatorDto.GreaterThan,
      ReferenceTypeDto.Value,
      null, null, null, null, null,
      ticker, "MOEX", TimeFrame.Minute
    );

  private static RuleGroupDto GroupWith(params RuleConditionDto[] conditions) =>
    new(Guid.NewGuid(), LogicalOperatorDto.And, conditions, []);

  // Asserts a thrown ValidationException carries exactly one structured error with the given
  // code, and returns it so the caller can assert its arguments.
  private static ValidationError AssertSingleCode(ValidationException ex, string code)
  {
    var error = Assert.Single(ex.ErrorCodes ?? []);
    Assert.Equal(code, error.Code);
    return error;
  }

  [Fact]
  public void ValidateConditions_ValidTemplates_Passes()
  {
    var group = GroupWith(
      Condition("SMA", """{"Periods": 20, "Deviation": 0.0005, "TimeFrame": 0}"""),
      Condition("Gap", """{"Percent": 1.0, "TimeFrame": 2}""")
    );

    RuleValidation.ValidateConditions(Serializer, group);
  }

  [Fact]
  public void ValidateConditions_ZeroGapPercent_Throws()
  {
    var group = GroupWith(Condition("Gap", """{"Percent": 0, "TimeFrame": 2}"""));

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.ValidateConditions(Serializer, group)),
      "template.paramPositive");

    Assert.Equal("Percent", error.Args["parameter"]);
    Assert.Equal("Gap", error.Args["indicator"]);
    Assert.Equal("SBER", error.Args["ticker"]);
  }

  [Fact]
  public void ValidateConditions_ZeroPeriod_Throws()
  {
    var group = GroupWith(Condition("SMA", """{"Periods": 0, "TimeFrame": 0}"""));

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.ValidateConditions(Serializer, group)),
      "template.paramMin");

    Assert.Equal("Periods", error.Args["parameter"]);
    Assert.Equal(1, error.Args["min"]);
    Assert.Equal("SMA", error.Args["indicator"]);
    Assert.Equal("SBER", error.Args["ticker"]);
  }

  [Fact]
  public void ValidateConditions_NegativeDeviation_Throws()
  {
    var group = GroupWith(Condition("SMA", """{"Periods": 20, "Deviation": -0.01, "TimeFrame": 0}"""));

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.ValidateConditions(Serializer, group)),
      "template.paramNonNegative");

    Assert.Equal("Deviation", error.Args["parameter"]);
    Assert.Equal("SMA", error.Args["indicator"]);
  }

  [Fact]
  public void ValidateConditions_ZeroTolerancesAllowed_Passes()
  {
    // Deviation / MinChannel / MaxChannel may legitimately be 0
    var group = GroupWith(Condition(
      "StochRSI",
      """{"RsiPeriod": 14, "StochPeriod": 14, "SignalPeriod": 3, "SmoothPeriod": 3, "Deviation": 0, "MinChannel": 0, "MaxChannel": 100, "TimeFrame": 0}"""
    ));

    RuleValidation.ValidateConditions(Serializer, group);
  }

  [Fact]
  public void ValidateConditions_MissingNumericParameter_Throws()
  {
    // An omitted parameter deserializes to its C# default (0) and must be rejected
    var group = GroupWith(Condition("SMA", """{"TimeFrame": 0}"""));

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.ValidateConditions(Serializer, group)),
      "template.paramMin");

    Assert.Equal("Periods", error.Args["parameter"]);
    Assert.Equal("SMA", error.Args["indicator"]);
  }

  [Fact]
  public void Validate_EmptyGroup_CarriesStructuredCode()
  {
    var ex = Assert.Throws<ValidationException>(() => RuleValidation.Validate(new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.And, [], [])));
    AssertSingleCode(ex, "rule.atLeastOneCondition");
  }

  [Fact]
  public void Validate_NestingTooDeep_CarriesStructuredCode()
  {
    // One level past the ceiling (RuleValidation.MaxGroupDepth nested groups); the depth
    var deepest = LeafGroup();
    for (var i = 0; i < RuleValidation.MaxGroupDepth + 1; i++)
      deepest = new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.And, [], [deepest]);

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.Validate(deepest)),
      "rule.nestingTooDeep");

    Assert.Equal(RuleValidation.MaxGroupDepth + 1, error.Args["max"]);
  }

  [Fact]
  public void Validate_TooManyConditions_CarriesStructuredCode()
  {
    // One leaf over the ceiling (RuleValidation.MaxTotalConditions)
    var conditions = Enumerable
      .Range(0, RuleValidation.MaxTotalConditions + 1)
      .Select(_ => Condition("SMA", null))
      .ToArray();

    var group = GroupWith(conditions);

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.Validate(group)),
      "rule.conditionsLimit");

    Assert.Equal(RuleValidation.MaxTotalConditions, error.Args["max"]);
    Assert.Equal(RuleValidation.MaxTotalConditions + 1, error.Args["found"]);
  }

  [Fact]
  public void Validate_ConditionsAboveCeiling_Throws()
  {
    var conditions = Enumerable
      .Range(0, RuleValidation.MaxTotalConditions + 1)
      .Select(_ => Condition("SMA", null))
      .ToArray();

    var ex = Assert.Throws<ValidationException>(
      () => RuleValidation.Validate(GroupWith(conditions)));

    Assert.Contains($"{RuleValidation.MaxTotalConditions} conditions", ex.Message);
  }

  [Fact]
  public void Validate_ConditionsUpToCeiling_Passes()
  {
    var conditions = Enumerable
      .Range(0, RuleValidation.MaxTotalConditions)
      .Select(_ => Condition("SMA", null))
      .ToArray();

    RuleValidation.Validate(GroupWith(conditions));
  }

  [Fact]
  public void Validate_MissingTicker_CarriesStructuredCode()
  {
    var group = new RuleGroupDto(
      Guid.NewGuid(),
      LogicalOperatorDto.And,
      [
        new RuleConditionDto(
          Guid.NewGuid(), "SMA", null,
          ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value,
          null, null, null, null, null, "", null, TimeFrame.Minute)
      ],
      []
    );

    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.Validate(group)),
      "rule.instrumentRequired");

    Assert.Empty(error.Args);
  }

  [Fact]
  public void ValidateName_TooLong_CarriesStructuredCode()
  {
    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.ValidateName(new string('a', RuleValidation.MaxRuleNameLength + 1))),
      "rule.nameTooLong");

    Assert.Equal(RuleValidation.MaxRuleNameLength, error.Args["max"]);
  }

  [Fact]
  public void ValidateName_Empty_CarriesStructuredCode()
  {
    AssertSingleCode(Assert.Throws<ValidationException>(() => RuleValidation.ValidateName("   ")), "rule.nameRequired");
  }

  [Fact]
  public void ValidateActions_DuplicateType_CarriesStructuredCode()
  {
    var error = AssertSingleCode(
      Assert.Throws<ValidationException>(() => RuleValidation.ValidateActions(
        [Action(ActionTypeDto.SendNotification), Action(ActionTypeDto.SendNotification)],
        null
      )),
      "rule.duplicateAction");

    Assert.Equal(nameof(ActionTypeDto.SendNotification), error.Args["type"]);
  }
}
