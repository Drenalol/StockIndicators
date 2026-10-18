using Moq;
using Newtonsoft.Json;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

/// <summary>
/// What a trigger's chart draws: the condition as the alert recorded it, so the picture of a
/// delivered alert survives every later edit of the rule. Only rows written before the snapshot
/// still depend on the live rule tree, and their failure rides as a stable code.
/// </summary>
public class TriggerChartContextResolverTests
{
  private static ISerializer Serializer =>
    new DefaultSerializer(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }, Logger.None);

  private static RuleCondition Condition(Indicator indicator, string ticker, string? exchange, string? templateData = null) =>
    new()
    {
      Id = Guid.NewGuid(),
      Indicator = indicator,
      Ticker = ticker,
      ExchangeName = exchange,
      TemplateData = templateData,
      TimeFrame = TimeFrame.Day
    };

  /// <summary>The rule as the repository materializes it: the tree deserialized from RuleData.</summary>
  private static Rule RuleOf(params RuleCondition[] leaves)
  {
    var rule = new Rule
    {
      Id = Guid.NewGuid(),
      Name = "rule",
      When = new RuleGroup { LogicalOperator = LogicalOperator.And, Conditions = leaves.ToList() }
    };
    rule.When.Id = rule.Id;

    return rule;
  }

  private static Mock<IRuleRepository> RepositoryWith(Rule? rule)
  {
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(rule);

    return rules;
  }

  private static Subscription SubscriptionOf(Rule rule) =>
    new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RuleGroupId = rule.Id, Cooldown = TimeSpan.FromMinutes(1) };

  private static Task<TriggerChartResolution> Resolve(
    IRuleRepository rules,
    ISerializer serializer,
    TriggerHistory trigger,
    Subscription subscription) =>
    TriggerChartContextResolver.ResolveAsync(rules, serializer, Logger.None, trigger, subscription, CancellationToken.None);

  [Fact]
  public async Task Snapshot_RendersTheConditionAsTheAlertSaw_ItEvenAfterTheRuleWasEdited()
  {
    // The alert fired on Volume 20x; the user then retuned the very same condition (or the save
    // re-minted its id) - the delivered alert still charts the parameters that fired
    var fired = Condition(Indicator.Volume, "BTCUSD", "BINANCE", """{"Periods":20,"Multiplier":2,"TimeFrame":0}""");
    var rule = RuleOf(Condition(Indicator.Volume, "BTCUSD", "BINANCE", """{"Periods":50,"Multiplier":5,"TimeFrame":0}"""));
    var subscription = SubscriptionOf(rule);

    var resolution = await Resolve(
      RepositoryWith(rule).Object,
      Serializer,
      new TriggerHistory
      {
        SubscriptionId = subscription.Id,
        TriggeredConditionId = fired.Id,
        FiredConditionData = Serializer.Serialize(fired)
      },
      subscription);

    Assert.True(resolution.IsRenderable);
    Assert.Equal(Indicator.Volume, resolution.Context!.Indicator);
    Assert.Equal("BTCUSD", resolution.Context.Ticker);
    Assert.Equal("BINANCE", resolution.Context.ExchangeName);
    Assert.Equal(fired.TemplateData, resolution.Context.TemplateData);
    // The live tree is not consulted at all: the row that fired points at nothing now
    Assert.Null(rule.When.FindCondition(fired.Id));
  }

  [Fact]
  public async Task LegacyRow_WithoutSnapshot_ResolvesTheConditionOfTheLiveRule() =>
    await LegacyRowResolvesFromTheLiveTree(nested: false);

  [Fact]
  public async Task LegacyRow_WithoutSnapshot_ResolvesAConditionOfANestedGroup() =>
    await LegacyRowResolvesFromTheLiveTree(nested: true);

  private static async Task LegacyRowResolvesFromTheLiveTree(bool nested)
  {
    var fired = Condition(Indicator.SMA, "SBER", "MOEX");
    var rule = RuleOf();

    if (nested)
      rule.When.Groups = [new RuleGroup { Id = Guid.NewGuid(), LogicalOperator = LogicalOperator.Or, Conditions = [fired] }];
    else
      rule.When.Conditions = [fired];

    var subscription = SubscriptionOf(rule);

    var resolution = await Resolve(
      RepositoryWith(rule).Object,
      Serializer,
      new TriggerHistory { SubscriptionId = subscription.Id, TriggeredConditionId = fired.Id },
      subscription);

    Assert.True(resolution.IsRenderable);
    Assert.Equal(Indicator.SMA, resolution.Context!.Indicator);
    Assert.Equal("SBER", resolution.Context.Ticker);
  }

  [Fact]
  public async Task LegacyRow_WhoseConditionLeftTheRule_ReportsThatTheConditionWasRemoved()
  {
    var rule = RuleOf(Condition(Indicator.SMA, "SBER", "MOEX"));
    var subscription = SubscriptionOf(rule);

    var resolution = await Resolve(
      RepositoryWith(rule).Object,
      Serializer,
      new TriggerHistory { SubscriptionId = subscription.Id, TriggeredConditionId = Guid.NewGuid() },
      subscription);

    Assert.False(resolution.IsRenderable);
    Assert.Equal(TriggerChartResolution.ConditionRemovedCode, resolution.FailureCode);
  }

  [Fact]
  public async Task AlertWithoutAFiredCondition_ReportsThatNoneWasRecorded()
  {
    // A notification collected on a false branch never had a condition to chart
    var rule = RuleOf(Condition(Indicator.SMA, "SBER", "MOEX"));
    var subscription = SubscriptionOf(rule);

    var resolution = await Resolve(
      RepositoryWith(rule).Object,
      Serializer,
      new TriggerHistory { SubscriptionId = subscription.Id },
      subscription);

    Assert.False(resolution.IsRenderable);
    Assert.Equal(TriggerChartResolution.NoFiredConditionCode, resolution.FailureCode);
  }

  [Fact]
  public async Task UnreadableSnapshot_FallsBackToTheRecordedConditionId()
  {
    var fired = Condition(Indicator.SMA, "SBER", "MOEX");
    var rule = RuleOf(fired);
    var subscription = SubscriptionOf(rule);

    var resolution = await Resolve(
      RepositoryWith(rule).Object,
      Serializer,
      new TriggerHistory
      {
        SubscriptionId = subscription.Id,
        TriggeredConditionId = fired.Id,
        FiredConditionData = """{"SomethingElse":1}"""
      },
      subscription);

    Assert.True(resolution.IsRenderable);
    Assert.Equal("SBER", resolution.Context!.Ticker);
  }

  [Fact]
  public async Task MissingRule_ReportsThatTheConditionWasRemoved()
  {
    var rule = RuleOf(Condition(Indicator.SMA, "SBER", "MOEX"));
    var subscription = SubscriptionOf(rule);

    var resolution = await Resolve(
      RepositoryWith(null).Object,
      Serializer,
      new TriggerHistory { SubscriptionId = subscription.Id, TriggeredConditionId = Guid.NewGuid() },
      subscription);

    Assert.False(resolution.IsRenderable);
    Assert.Equal(TriggerChartResolution.ConditionRemovedCode, resolution.FailureCode);
  }
}
