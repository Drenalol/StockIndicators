using MediatR;
using Moq;
using Newtonsoft.Json;
using Serilog;
using LazyCache;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Infrastructure.Data;
using StockIndicators.Infrastructure.Domain.Indicators;
using Xunit;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;
using ExampleWindow = StockIndicators.Application.Charts.ExampleMarketData.ExampleWindow;

namespace StockIndicators.Indicators.Tests.Rules;

/// <summary>
/// End-to-end evaluation of multi-instrument rules: the REAL stack (CompositeConditionEvaluator →
/// SimpleConditionEvaluator → real indicator handlers) over the REAL market fixtures the example
/// charts replay (an AAPL window ending on an SMA cross, a TSLA window ending on a gap). Proves a
/// rule combining instruments of a condition tree works on each condition's own data — the leaf
/// mocks of CompositeConditionEvaluatorTests only ever proved the boolean fold.
/// </summary>
public class MultiInstrumentEvaluationTests
{
  private static readonly ExampleWindow AaplSma = Window(AppIndicator.SMA);
  private static readonly ExampleWindow TslaGap = Window(AppIndicator.Gap);

  private readonly InMemoryCandleStore _store = new();
  private readonly SimpleConditionEvaluator _leafEvaluator;
  private readonly CompositeConditionEvaluator _evaluator;

  public MultiInstrumentEvaluationTests()
  {
    ISerializer serializer = new DefaultSerializer(
      new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error },
      Log.Logger);

    _leafEvaluator = new SimpleConditionEvaluator(BuildMediator(), _store, serializer);
    _evaluator = new CompositeConditionEvaluator(_leafEvaluator);
  }

  private static ExampleWindow Window(AppIndicator indicator)
  {
    Assert.True(ExampleMarketData.TryGetWindow(indicator, out var window), $"{indicator}: no example window");

    return window;
  }

  /// <summary>
  /// Routes indicator commands to the production handlers with one shared state cache —
  /// exactly the runtime wiring, just without MediatR's container lookup.
  /// </summary>
  private IMediator BuildMediator()
  {
    IIndicatorCache cache = new IndicatorInMemoryCache(new CachingService());

    var mediator = new Mock<IMediator>();

    mediator
      .Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
      .Returns(async (object command, CancellationToken cancellationToken) => command switch
      {
        SmaIndicatorCommand sma => await new SmaIndicatorCommandHandler(cache, Log.Logger).Handle(sma, cancellationToken),
        GapIndicatorCommand gap => await new GapIndicatorCommandHandler(cache, Log.Logger).Handle(gap, cancellationToken),
        _ => throw new InvalidOperationException($"unexpected command {command.GetType().Name}")
      });

    return mediator.Object;
  }

  private static RuleCondition SmaCondition(ExampleWindow window) =>
    new()
    {
      Id = Guid.NewGuid(),
      Indicator = AppIndicator.SMA,
      Ticker = window.Ticker,
      ExchangeName = window.Candles[0].ExchangeName,
      TimeFrame = TimeFrame.Day,
      TemplateData = """{"Periods":20,"TimeFrame":"Day"}""",
    };

  private static RuleCondition GapCondition(ExampleWindow window) =>
    new()
    {
      Id = Guid.NewGuid(),
      Indicator = AppIndicator.Gap,
      Ticker = window.Ticker,
      ExchangeName = window.Candles[0].ExchangeName,
      TimeFrame = TimeFrame.Day,
      TemplateData = """{"Percent":2}""",
    };

  private static RuleGroup Group(LogicalOperator @operator, params RuleCondition[] conditions) =>
    new()
    {
      Id = Guid.NewGuid(),
      LogicalOperator = @operator,
      Conditions = [.. conditions],
    };

  private void Store(ExampleWindow window, int cut = 0) =>
    _store.Store(window.Ticker, cut == 0 ? window.Candles : window.Candles[..^cut]);

  [Fact]
  public async Task And_TwoInstruments_FiresWhenBothConditionsFireOnTheirOwnData()
  {
    Store(AaplSma);
    Store(TslaGap);

    var evaluation = await _evaluator.EvaluateGroupAsync(
      Group(LogicalOperator.And, SmaCondition(AaplSma), GapCondition(TslaGap)), UserLocale.Default);

    Assert.True(evaluation.Met);
    Assert.Equal(2, evaluation.MetConditions.Count);
    // Each condition pulled its OWN instrument's series — the same ticker would never produce both
    Assert.Contains("AAPL", _store.Fetches.Select(fetch => fetch.Ticker));
    Assert.Contains("TSLA", _store.Fetches.Select(fetch => fetch.Ticker));
  }

  [Fact]
  public async Task And_TwoInstruments_NotMetWhenOnlyTheFirstInstrumentFires()
  {
    Store(AaplSma);
    Store(TslaGap, cut: 1); // drop the TSLA window's triggering candle: no gap on its last pair

    var evaluation = await _evaluator.EvaluateGroupAsync(
      Group(LogicalOperator.And, SmaCondition(AaplSma), GapCondition(TslaGap)), UserLocale.Default);

    Assert.False(evaluation.Met);
    var aaplOutcome = evaluation.Conditions.Single(outcome => outcome.Condition.Ticker == "AAPL");
    Assert.True(aaplOutcome.Met); // the AAPL side is unaffected by the TSLA data
  }

  [Fact]
  public async Task And_TwoInstruments_NotMetWhenOnlyTheSecondInstrumentFires()
  {
    Store(AaplSma, cut: 1); // drop the AAPL window's cross candle
    Store(TslaGap);

    var evaluation = await _evaluator.EvaluateGroupAsync(
      Group(LogicalOperator.And, SmaCondition(AaplSma), GapCondition(TslaGap)), UserLocale.Default);

    Assert.False(evaluation.Met);
    var tslaOutcome = evaluation.Conditions.Single(outcome => outcome.Condition.Ticker == "TSLA");
    Assert.True(tslaOutcome.Met); // the TSLA side is unaffected by the AAPL data
  }

  [Fact]
  public async Task Or_TwoInstruments_FiresFromEitherSide()
  {
    Store(AaplSma);
    Store(TslaGap, cut: 1);

    var evaluation = await _evaluator.EvaluateGroupAsync(
      Group(LogicalOperator.Or, SmaCondition(AaplSma), GapCondition(TslaGap)), UserLocale.Default);

    Assert.True(evaluation.Met);
  }

  [Fact]
  public async Task Or_TwoInstruments_NotMetWhenNeitherSideFires()
  {
    Store(AaplSma, cut: 1);
    Store(TslaGap, cut: 1);

    var evaluation = await _evaluator.EvaluateGroupAsync(
      Group(LogicalOperator.Or, SmaCondition(AaplSma), GapCondition(TslaGap)), UserLocale.Default);

    Assert.False(evaluation.Met);
  }

  [Fact]
  public async Task And_MissingDataForSecondInstrument_ConditionIsSilentlyNotMet()
  {
    Store(AaplSma);
    // TSLA has no candles at all (cold ticker whose history is not there yet)

    var evaluation = await _evaluator.EvaluateGroupAsync(
      Group(LogicalOperator.And, SmaCondition(AaplSma), GapCondition(TslaGap)), UserLocale.Default);

    Assert.False(evaluation.Met);
    Assert.True(evaluation.Conditions.Single(outcome => outcome.Condition.Ticker == "AAPL").Met);
  }

  [Fact]
  public async Task NestedGroups_PerInstrumentLeavesEvaluateInsideTheirOwnBranches()
  {
    Store(AaplSma);
    Store(TslaGap, cut: 1);

    // AND[AAPL SMA fires, OR[TSLA gap does not fire, second AAPL SMA condition fires]]
    var root = Group(LogicalOperator.And, SmaCondition(AaplSma));
    root.Groups.Add(Group(LogicalOperator.Or, GapCondition(TslaGap), SmaCondition(AaplSma)));

    var evaluation = await _evaluator.EvaluateGroupAsync(root, UserLocale.Default);

    Assert.True(evaluation.Met);
    Assert.Equal(3, evaluation.Conditions.Count);

    // Same tree with the AAPL data truncated: the AAPL leaves go silent and the AND fails
    Store(AaplSma, cut: 1);

    var truncated = await _evaluator.EvaluateGroupAsync(root, UserLocale.Default);

    Assert.False(truncated.Met);
  }

  /// <summary>
  /// Minimal candle store: per-ticker series, GetLastCandlesAsync returns the last `periods`
  /// candles, and every fetch is recorded so tests can prove per-condition data access.
  /// </summary>
  private sealed class InMemoryCandleStore : ICandleRepository
  {
    private readonly Dictionary<string, Candle[]> _byTicker = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Ticker, int Periods)> Fetches { get; } = [];

    public void Store(string ticker, Candle[] candles) => _byTicker[ticker] = candles;

    public Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Candle[]> GetLastCandlesAsync(string ticker, string? exchangeName, int periods, TimeFrame timeFrame, CancellationToken cancellationToken = default)
    {
      Fetches.Add((ticker, periods));

      if (!_byTicker.TryGetValue(ticker, out var candles))
        return Task.FromResult(Array.Empty<Candle>());

      return Task.FromResult(candles.Length <= periods ? candles : candles[^periods..]);
    }

    public Task<Candle[]> GetCandlesAsync(string ticker, string? exchangeName, DateTime start, DateTime end, TimeFrame timeFrame, CancellationToken cancellationToken = default) =>
      Task.FromResult(Array.Empty<Candle>());
  }
}
