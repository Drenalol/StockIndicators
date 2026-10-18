using StockIndicators.Domain.Models.Rules;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

/// <summary>
/// Pure unit tests over the WHEN-tree instrument projection: <see cref="RuleGroup.CollectInstruments"/>
/// feeds the RuleInstrument table (exact, case-sensitive identity), while
/// <see cref="RuleGroup.WatchesMultipleInstruments"/> decides whether alert lines carry tickers
/// (case-insensitive, so the same ticker in different casings is still one instrument).
/// </summary>
public class RuleInstrumentProjectionTests
{
  private static RuleCondition Condition(string ticker, string? exchange = null) =>
    new() { Id = Guid.NewGuid(), Ticker = ticker, ExchangeName = exchange };

  private static RuleGroup Group(RuleCondition[] conditions, RuleGroup[]? groups = null) =>
    new() { Id = Guid.NewGuid(), LogicalOperator = LogicalOperator.And, Conditions = conditions.ToList(), Groups = groups?.ToList() ?? [] };

  private static (string Ticker, string ExchangeName)[] Sorted(IEnumerable<RuleInstrument> instruments) =>
    instruments
      .Select(i => (i.Ticker, i.ExchangeName))
      .OrderBy(p => p.Ticker, StringComparer.Ordinal)
      .ThenBy(p => p.ExchangeName, StringComparer.Ordinal)
      .ToArray();

  [Fact]
  public void CollectInstruments_GathersDistinctPairsAcrossNestedGroups()
  {
    var tree = Group(
      [Condition("AAPL", "NASDAQ"), Condition("AAPL", "NASDAQ"), Condition("SBER")],
      [Group([Condition("TSLA", "NASDAQ")]), Group([Condition("AAPL", "MOEX")])]);

    var ruleId = Guid.NewGuid();

    var instruments = tree.CollectInstruments(ruleId);

    Assert.Equal(
      [("AAPL", "MOEX"), ("AAPL", "NASDAQ"), ("SBER", ""), ("TSLA", "NASDAQ")],
      Sorted(instruments));
    Assert.All(instruments, instrument => Assert.Equal(ruleId, instrument.RuleId));
  }

  [Fact]
  public void CollectInstruments_DeduplicatesCaseSensitively()
  {
    var tree = Group([Condition("AAPL"), Condition("aapl"), Condition("AAPL")]);

    var instruments = tree.CollectInstruments(Guid.NewGuid());

    Assert.Equal(["AAPL", "aapl"], instruments.Select(i => i.Ticker).OrderBy(t => t, StringComparer.Ordinal).ToArray());
  }

  [Fact]
  public void CollectInstruments_NormalizesMissingExchangeToEmptyString()
  {
    var tree = Group([Condition("BTCUSD")]);

    var instrument = Assert.Single(tree.CollectInstruments(Guid.NewGuid()));

    Assert.Equal(string.Empty, instrument.ExchangeName);
    Assert.Null(instrument.ToKey().ExchangeName);
  }

  [Fact]
  public void CollectInstruments_EmptyTree_YieldsNothing()
  {
    Assert.Empty(Group([]).CollectInstruments(Guid.NewGuid()));
  }

  [Fact]
  public void WatchesMultipleInstruments_SingleInstrument_False()
  {
    var tree = Group([Condition("AAPL", "NASDAQ"), Condition("AAPL", "NASDAQ")]);

    Assert.False(tree.WatchesMultipleInstruments());
  }

  [Fact]
  public void WatchesMultipleInstruments_CaseDifferentDuplicates_CountAsOne()
  {
    var tree = Group([Condition("AAPL"), Condition("aapl")]);

    Assert.False(tree.WatchesMultipleInstruments());
  }

  [Fact]
  public void WatchesMultipleInstruments_TwoInstruments_True()
  {
    // Same ticker on two exchanges is still two instruments
    var tree = Group([Condition("AAPL"), Condition("AAPL", "NASDAQ")]);

    Assert.True(tree.WatchesMultipleInstruments());
  }

  [Fact]
  public void WatchesMultipleInstruments_EmptyTree_False()
  {
    Assert.False(Group([]).WatchesMultipleInstruments());
  }
}
