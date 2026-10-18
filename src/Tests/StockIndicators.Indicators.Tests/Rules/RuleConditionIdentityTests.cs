using Moq;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Rules.UpdateRule;
using StockIndicators.Contracts.Candles;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

/// <summary>
/// A condition's id is the identity other stored state points at: the trigger history's
/// <see cref="TriggerHistory.TriggeredConditionId"/> (an on-demand trigger chart re-resolves the
/// fired condition from the live rule tree), the per-condition trailing-stop anchors and the
/// same-candle alert dedup. A save that re-mints ids orphans all three, so the ids a client
/// sends are the ids that get stored - the write path never re-mints a leaf the client named.
/// </summary>
public class RuleConditionIdentityTests
{
  private readonly Guid _userId = Guid.NewGuid();

  private static ISerializer Serializer =>
    new DefaultSerializer(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }, Log.Logger);

  private Rule ExistingRule(Guid firedConditionId)
  {
    var rule = new Rule
    {
      Id = Guid.NewGuid(),
      UserId = _userId,
      Name = "rule",
      When = new RuleGroup
      {
        LogicalOperator = LogicalOperator.And,
        Conditions = [new RuleCondition { Id = firedConditionId, Indicator = Indicator.EMA, Ticker = "AAPL" }]
      }
    };
    rule.When.Id = rule.Id;
    return rule;
  }

  private static RuleConditionDto Condition(Guid? id, string indicator, string ticker) =>
    new(id, indicator, null, ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value,
      null, null, null, 1m, null, ticker, "NASDAQ", TimeFrame.Day);

  private Subscription OwnedSubscription(Guid ruleGroupId) =>
    new() { Id = Guid.NewGuid(), UserId = _userId, RuleGroupId = ruleGroupId, Cooldown = TimeSpan.FromMinutes(1) };

  private Mock<IRuleRepository> RulesMockedFor(Rule rule, Action<Rule> onPersist)
  {
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
    rules.Setup(r => r.GetInstrumentsAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    rules
      .Setup(r => r.UpdateAsync(It.IsAny<Rule>(), It.IsAny<CancellationToken>()))
      .Callback((Rule r, CancellationToken _) => onPersist(r))
      .Returns(Task.CompletedTask);

    return rules;
  }

  private Task Save(Mock<IRuleRepository> rules, Guid ruleId, RuleGroupDto when) =>
    new UpdateRuleCommandHandler(
        rules.Object,
        new Mock<ISubscriptionRepository>().Object,
        new Mock<ITriggerSnapshotRepository>().Object,
        Serializer)
      .Handle(new UpdateRuleCommand(_userId, ruleId, "rule", when), CancellationToken.None);

  [Fact]
  public async Task Update_SentConditionIds_SurviveTheRebuild_TheFiredTriggerStillResolves()
  {
    var firedConditionId = Guid.NewGuid();
    var rule = ExistingRule(firedConditionId);
    Rule? saved = null;
    var rules = RulesMockedFor(rule, r => saved = r);

    // The editor's save payload: the leaf ids it was handed when the form opened (the fired
    // EMA in the root group, an RSI in a nested group) plus one condition the user added
    // with a null id - that one is the server's to mint
    var addedConditionId = Guid.NewGuid();
    var when = new RuleGroupDto(rule.Id, LogicalOperatorDto.And,
      [Condition(firedConditionId, "EMA", "MSFT")],
      [new RuleGroupDto(Guid.NewGuid(), LogicalOperatorDto.Or,
        [Condition(addedConditionId, "RSI", "MSFT"), Condition(null, "SMA", "MSFT")], [])]);

    await Save(rules, rule.Id, when);

    Assert.NotNull(saved);

    // Read the stored tree back the way the repository does it (the jsonb payload, not the
    // in-memory object the handler just built)
    var payload = (RuleDataPayload)Serializer.DeserializeOrNull(saved!.RuleData, typeof(RuleDataPayload))!;
    var storedIds = payload.When.Conditions.Select(c => c.Id)
      .Concat(payload.When.Groups.SelectMany(g => g.Conditions).Select(c => c.Id))
      .ToArray();

    Assert.Contains(firedConditionId, storedIds);
    Assert.Contains(addedConditionId, storedIds);
    Assert.DoesNotContain(Guid.Empty, storedIds);

    // The alert that fired before the edit keeps pointing at a live leaf, so its chart still
    // renders - including a condition of a nested group
    var subscription = OwnedSubscription(rule.Id);
    rules.Setup(r => r.GetByIdAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);

    foreach (var conditionId in new[] { firedConditionId, addedConditionId })
    {
      var resolution = await TriggerChartContextResolver.ResolveAsync(
        rules.Object,
        Serializer,
        Logger.None,
        new TriggerHistory { SubscriptionId = subscription.Id, TriggeredConditionId = conditionId },
        subscription,
        CancellationToken.None);

      Assert.True(resolution.IsRenderable);
      Assert.Equal("MSFT", resolution.Context!.Ticker);
      Assert.Equal("NASDAQ", resolution.Context.ExchangeName);
    }
  }

  [Fact]
  public async Task Update_WithoutConditionIds_RemintsEveryLeaf_TheFiredTriggerIsOrphaned()
  {
    // The contract the frontend has to honour: an anonymous payload is a brand-new tree, so
    // the alert that fired before the save no longer resolves to a condition and its chart 404s
    var firedConditionId = Guid.NewGuid();
    var rule = ExistingRule(firedConditionId);
    Rule? saved = null;
    var rules = RulesMockedFor(rule, r => saved = r);

    await Save(rules, rule.Id, new RuleGroupDto(null, LogicalOperatorDto.And,
      [Condition(null, "EMA", "MSFT")], []));

    var subscription = OwnedSubscription(rule.Id);
    rules.Setup(r => r.GetByIdAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved!);

    var resolution = await TriggerChartContextResolver.ResolveAsync(
      rules.Object,
      Serializer,
      Logger.None,
      new TriggerHistory { SubscriptionId = subscription.Id, TriggeredConditionId = firedConditionId },
      subscription,
      CancellationToken.None);

    Assert.False(resolution.IsRenderable);
    Assert.Equal(TriggerChartResolution.ConditionRemovedCode, resolution.FailureCode);
  }
}
