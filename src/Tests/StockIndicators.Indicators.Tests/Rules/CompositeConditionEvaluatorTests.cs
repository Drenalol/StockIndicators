using Moq;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

public class CompositeConditionEvaluatorTests
{
  private readonly Mock<IConditionEvaluator> _leafMock;
  private readonly CompositeConditionEvaluator _evaluator;

  public CompositeConditionEvaluatorTests()
  {
    _leafMock = new Mock<IConditionEvaluator>(MockBehavior.Strict);
    _evaluator = new CompositeConditionEvaluator(_leafMock.Object);
  }

  private static RuleCondition Condition(string ticker) => new()
  {
    Id = Guid.NewGuid(),
    Indicator = Indicator.SMA,
    Ticker = ticker
  };

  private void SetupLeaf(string ticker, bool result) =>
    _leafMock
      .Setup(leaf => leaf.EvaluateAsync(It.Is<RuleCondition>(condition => condition.Ticker == ticker), It.IsAny<UserLocale>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .Returns((RuleCondition condition, UserLocale _, bool __, CancellationToken ___) => Task.FromResult(new ConditionOutcome(condition, result, null)));

  [Fact]
  public async Task AndGroup_BothMet_ReturnsTrue()
  {
    SetupLeaf("AAA", true);
    SetupLeaf("BBB", true);

    var group = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.And,
      Conditions = [Condition("AAA"), Condition("BBB")]
    };

    Assert.True((await _evaluator.EvaluateGroupAsync(group, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task AndGroup_OneNotMet_ReturnsFalse()
  {
    SetupLeaf("AAA", true);
    SetupLeaf("BBB", false);

    var group = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.And,
      Conditions = [Condition("AAA"), Condition("BBB")]
    };

    Assert.False((await _evaluator.EvaluateGroupAsync(group, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task OrGroup_OneMet_ReturnsTrue()
  {
    SetupLeaf("AAA", false);
    SetupLeaf("BBB", true);

    var group = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.Or,
      Conditions = [Condition("AAA"), Condition("BBB")]
    };

    Assert.True((await _evaluator.EvaluateGroupAsync(group, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task OrGroup_NoneMet_ReturnsFalse()
  {
    SetupLeaf("AAA", false);
    SetupLeaf("BBB", false);

    var group = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.Or,
      Conditions = [Condition("AAA"), Condition("BBB")]
    };

    Assert.False((await _evaluator.EvaluateGroupAsync(group, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task EmptyGroup_ReturnsFalse()
  {
    var group = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.And
    };

    Assert.False((await _evaluator.EvaluateGroupAsync(group, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task NestedGroups_CombinedOperators_ReturnsExpected()
  {
    // (AAA AND BBB) OR CCC
    SetupLeaf("AAA", true);
    SetupLeaf("BBB", false);
    SetupLeaf("CCC", true);

    var inner = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.And,
      Conditions = [Condition("AAA"), Condition("BBB")]
    };

    var root = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.Or,
      Conditions = [Condition("CCC")],
      Groups = [inner]
    };

    Assert.True((await _evaluator.EvaluateGroupAsync(root, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task NestedGroups_AllBranchesFalse_ReturnsFalse()
  {
    SetupLeaf("AAA", false);
    SetupLeaf("BBB", false);
    SetupLeaf("CCC", false);

    var inner = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.And,
      Conditions = [Condition("AAA"), Condition("BBB")]
    };

    var root = new RuleGroup
    {
      Id = Guid.NewGuid(),
      LogicalOperator = LogicalOperator.Or,
      Conditions = [Condition("CCC")],
      Groups = [inner]
    };

    Assert.False((await _evaluator.EvaluateGroupAsync(root, UserLocale.Default)).Met);
  }

  [Fact]
  public async Task EvaluateAsync_DelegatesToLeaf()
  {
    SetupLeaf("AAA", true);

    Assert.True((await _evaluator.EvaluateAsync(Condition("AAA"), UserLocale.Default)).Met);
  }
}
