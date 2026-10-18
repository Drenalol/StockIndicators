using System.Linq.Expressions;
using MediatR;
using Moq;
using Newtonsoft.Json;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Rules;
using StockIndicators.Application.Rules.DeleteRule;
using StockIndicators.Application.Rules.GetRule;
using StockIndicators.Application.Rules.GetRules;
using StockIndicators.Application.Rules.UpdateRule;
using StockIndicators.Contracts.Candles;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

/// <summary>
/// Every user-facing rule read is scoped by the owner id; a foreign rule is
/// indistinguishable from a missing one (RuleNotFound / 404), never a 403.
/// </summary>
public class RuleOwnershipTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly Guid _otherUserId = Guid.NewGuid();

  private static ISerializer Serializer =>
    new DefaultSerializer(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }, Log.Logger);

  private Rule OwnedRule(Guid userId)
  {
    var rule = new Rule
    {
      Id = Guid.NewGuid(),
      UserId = userId,
      Name = "rule",
      When = new RuleGroup
      {
        LogicalOperator = LogicalOperator.And,
        Conditions = [new RuleCondition { Id = Guid.NewGuid(), Indicator = Indicator.EMA, Ticker = "AAPL" }]
      }
    };
    rule.When.Id = rule.Id;
    return rule;
  }

  private static RuleGroupDto ValidWhen() =>
    new(null, LogicalOperatorDto.And,
      [new RuleConditionDto(null, "EMA", null, ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value,
        null, null, null, 1m, null, "AAPL", null, TimeFrame.Day)],
      []);

  private static async IAsyncEnumerable<Rule> ToAsync(IEnumerable<Rule> items)
  {
    await Task.CompletedTask;

    foreach (var item in items)
      yield return item;
  }

  // --- Get by id ---

  [Fact]
  public async Task Get_Owner_ReturnsRule()
  {
    var rule = OwnedRule(_userId);
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

    var dto = await new GetRuleQueryHandler(rules.Object).Handle(new GetRuleQuery(rule.Id, _userId), CancellationToken.None);

    Assert.Equal(rule.Id, dto.Id);
  }

  [Fact]
  public async Task Get_ForeignOwner_ThrowsNotFound()
  {
    var rule = OwnedRule(_otherUserId);
    var rules = new Mock<IRuleRepository>();
    // The rule exists only for its owner; the scoped read for the caller finds nothing
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _otherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

    await Assert.ThrowsAsync<RuleNotFoundException>(
      () => new GetRuleQueryHandler(rules.Object).Handle(new GetRuleQuery(rule.Id, _userId), CancellationToken.None));
  }

  // --- Get list ---

  [Fact]
  public async Task GetList_ReturnsOnlyOwnRules()
  {
    var mine = OwnedRule(_userId);
    var others = OwnedRule(_otherUserId);
    var all = new[] { mine, others };

    var rules = new Mock<IRuleRepository>();
    rules
      .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Rule, bool>>>()))
      .Returns((Expression<Func<Rule, bool>> predicate) => ToAsync(all.Where(rule => predicate.Compile()(rule))));

    var result = await new GetRulesQueryHandler(rules.Object).Handle(new GetRulesQuery(_userId), CancellationToken.None);

    Assert.Equal(new[] { mine.Id }, result.Select(dto => dto.Id));
  }

  // --- Update ---

  [Fact]
  public async Task Update_Owner_Succeeds()
  {
    var rule = OwnedRule(_userId);
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
    rules.Setup(r => r.GetInstrumentsAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    var subscriptions = new Mock<ISubscriptionRepository>();

    var handler = new UpdateRuleCommandHandler(rules.Object, subscriptions.Object, new Mock<ITriggerSnapshotRepository>().Object, Serializer);
    await handler.Handle(new UpdateRuleCommand(_userId, rule.Id, "rule", ValidWhen()), CancellationToken.None);

    // The ownership-scoped read is AsNoTracking: the handler must mark the entity via
    // UpdateAsync, otherwise SaveChanges writes nothing (PATCH silently discards the tree)
    rules.Verify(r => r.UpdateAsync(rule, It.IsAny<CancellationToken>()), Times.Once);
    rules.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

    // The previous instrument set is snapshotted before the rebuild and reconciled after commit
    rules.Verify(r => r.GetInstrumentsAsync(rule.Id, It.IsAny<CancellationToken>()), Times.Once);
    subscriptions.Verify(
      r => r.SyncInstrumentsAfterRuleChangeAsync(It.Is<IReadOnlyCollection<InstrumentKey>>(keys => keys.Count == 0), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task Update_PersistsNewTickerIntoRuleData()
  {
    var rule = OwnedRule(_userId);
    Rule? saved = null;
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
    rules.Setup(r => r.UpdateAsync(It.IsAny<Rule>(), It.IsAny<CancellationToken>()))
      .Callback((Rule r, CancellationToken _) => saved = r)
      .Returns(Task.CompletedTask);
    rules.Setup(r => r.GetInstrumentsAsync(rule.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

    var updatedWhen = new RuleGroupDto(null, LogicalOperatorDto.And,
      [new RuleConditionDto(null, "EMA", null, ConditionOperatorDto.GreaterThan, ReferenceTypeDto.Value,
        null, null, null, 1m, null, "TSLA", "NASDAQ", TimeFrame.Day)],
      []);

    var handler = new UpdateRuleCommandHandler(rules.Object, new Mock<ISubscriptionRepository>().Object, new Mock<ITriggerSnapshotRepository>().Object, Serializer);
    await handler.Handle(new UpdateRuleCommand(_userId, rule.Id, "rule", updatedWhen), CancellationToken.None);

    Assert.NotNull(saved);
    Assert.Contains("TSLA", saved!.RuleData);
    Assert.DoesNotContain("AAPL", saved.RuleData);
  }

  [Fact]
  public async Task Update_ForeignOwner_ThrowsNotFound()
  {
    var rule = OwnedRule(_otherUserId);
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _otherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
    var subscriptions = new Mock<ISubscriptionRepository>();

    var handler = new UpdateRuleCommandHandler(rules.Object, subscriptions.Object, new Mock<ITriggerSnapshotRepository>().Object, Serializer);

    await Assert.ThrowsAsync<RuleNotFoundException>(
      () => handler.Handle(new UpdateRuleCommand(_userId, rule.Id, "rule", ValidWhen()), CancellationToken.None));

    rules.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    subscriptions.Verify(r => r.SyncInstrumentsAfterRuleChangeAsync(It.IsAny<IReadOnlyCollection<InstrumentKey>>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  // --- Delete ---

  [Fact]
  public async Task Delete_Owner_Deletes()
  {
    var rule = OwnedRule(_userId);
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

    await new DeleteRuleCommandHandler(rules.Object).Handle(new DeleteRuleCommand(_userId, rule.Id), CancellationToken.None);

    rules.Verify(r => r.DeleteAsync(rule.Id, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Delete_ForeignOwner_ThrowsNotFound()
  {
    var rule = OwnedRule(_otherUserId);
    var rules = new Mock<IRuleRepository>();
    rules.Setup(r => r.GetByIdForUserAsync(rule.Id, _otherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

    await Assert.ThrowsAsync<RuleNotFoundException>(
      () => new DeleteRuleCommandHandler(rules.Object).Handle(new DeleteRuleCommand(_userId, rule.Id), CancellationToken.None));

    rules.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
  }
}
