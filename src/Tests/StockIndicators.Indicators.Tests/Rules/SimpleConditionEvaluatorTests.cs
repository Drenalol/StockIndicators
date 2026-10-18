using MediatR;
using Moq;
using Newtonsoft.Json;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Infrastructure.Data;
using Xunit;

namespace StockIndicators.Indicators.Tests.Rules;

public class SimpleConditionEvaluatorTests
{
  private readonly Mock<IMediator> _mediator;
  private readonly Mock<ICandleRepository> _candleRepository;
  private readonly SimpleConditionEvaluator _evaluator;
  private IndicatorCommandBase? _capturedCommand;

  public SimpleConditionEvaluatorTests()
  {
    _mediator = new Mock<IMediator>();
    _candleRepository = new Mock<ICandleRepository>();

    // Same serializer the production flat pipeline uses (Newtonsoft, missing-member = error)
    var serializer = new DefaultSerializer(
      new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error },
      Log.Logger);

    _evaluator = new SimpleConditionEvaluator(_mediator.Object, _candleRepository.Object, serializer);
  }

  // 25 by default: above the Periods=20 warmup of the test template, so the
  // insufficient-candles guard lets the happy-path tests reach the mediator.
  private static Candle[] Candles(int count = 25) =>
    Enumerable.Range(0, count)
      .Select(i => new Candle("MSFT", DateTime.UtcNow.AddDays(-i), TimeFrame.Day, 100 + i, 102 + i, 99 + i, 101 + i, 1_000_000, true, true, "NASDAQ"))
      .ToArray();

  private void SetupCandles() =>
    _candleRepository
      .Setup(repo => repo.GetLastCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Candles());

  private void SetupMediator(bool triggered) =>
    _mediator
      .Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
      .Callback<object, CancellationToken>((request, _) => _capturedCommand = request as IndicatorCommandBase)
      .ReturnsAsync((object)new MaIndicatorResult { IsTriggered = triggered, Result = 100, Test = MaTest.None });

  // "CandlePart" is a string in the stored template JSON; System.Text.Json would reject it.
  // This guards the switch to the shared Newtonsoft serializer and the Template enrichment.
  [Fact]
  public async Task EvaluateAsync_SmaWithCandlePartString_SetsTemplateAndEvaluates()
  {
    SetupCandles();
    SetupMediator(triggered: true);

    var condition = new RuleCondition
    {
      Id = Guid.NewGuid(),
      Indicator = Indicator.SMA,
      Ticker = "MSFT",
      ExchangeName = "NASDAQ",
      TimeFrame = TimeFrame.Day,
      TemplateData = "{\"Periods\":20,\"TimeFrame\":2,\"CandlePart\":\"Close\",\"Deviation\":0.0005}"
    };

    var outcome = await _evaluator.EvaluateAsync(condition, UserLocale.Default);

    Assert.True(outcome.Met);
    Assert.NotNull(outcome.Value);
    var template = Assert.IsType<MaIndicatorTemplate>(_capturedCommand!.Template);
    Assert.Equal(20, template.Periods);
    Assert.Equal(CandlePart.Close, template.CandlePart);
    Assert.Equal(TimeFrame.Day, template.TimeFrame);

    // The indicator state slot is the condition id, so every leaf keeps its own state
    Assert.Equal(condition.Id, _capturedCommand.SubscriptionId);
    Assert.Equal("MSFT", _capturedCommand.Ticker);
  }

  [Fact]
  public async Task EvaluateAsync_NoCandles_ReturnsFalse()
  {
    _candleRepository
      .Setup(repo => repo.GetLastCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<Candle>());

    var condition = new RuleCondition
    {
      Id = Guid.NewGuid(),
      Indicator = Indicator.SMA,
      Ticker = "MSFT",
      ExchangeName = "NASDAQ",
      TimeFrame = TimeFrame.Day,
      TemplateData = "{\"Periods\":20,\"TimeFrame\":2,\"CandlePart\":\"Close\",\"Deviation\":0.0005}"
    };

    var outcome = await _evaluator.EvaluateAsync(condition, UserLocale.Default);

    Assert.False(outcome.Met);
    Assert.Null(outcome.Value);
  }

  [Fact]
  public async Task EvaluateAsync_FewerCandlesThanPeriods_SkipsEvaluation()
  {
    _candleRepository
      .Setup(repo => repo.GetLastCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Candles(5));

    var condition = new RuleCondition
    {
      Id = Guid.NewGuid(),
      Indicator = Indicator.SMA,
      Ticker = "MSFT",
      ExchangeName = "NASDAQ",
      TimeFrame = TimeFrame.Day,
      TemplateData = "{\"Periods\":20,\"TimeFrame\":2,\"CandlePart\":\"Close\",\"Deviation\":0.0005}"
    };

    var outcome = await _evaluator.EvaluateAsync(condition, UserLocale.Default);

    Assert.False(outcome.Met);
    Assert.Null(outcome.Value);
    _mediator.Verify(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Theory]
  [InlineData(UserLocale.English, "100.0000 (none)")]
  [InlineData(UserLocale.Russian, "100.0000 (без пересечения)")]
  public async Task EvaluateAsync_ValueTextIsLocalized(string language, string expectedValue)
  {
    SetupCandles();
    SetupMediator(triggered: true);

    var condition = new RuleCondition
    {
      Id = Guid.NewGuid(),
      Indicator = Indicator.SMA,
      Ticker = "MSFT",
      ExchangeName = "NASDAQ",
      TimeFrame = TimeFrame.Day,
      TemplateData = "{\"Periods\":20,\"TimeFrame\":2,\"CandlePart\":\"Close\",\"Deviation\":0.0005}"
    };

    var locale = new UserLocale(language, TimeZoneInfo.Utc);

    var outcome = await _evaluator.EvaluateAsync(condition, locale);

    Assert.Equal(expectedValue, outcome.Value);
  }

  // A post-/pre-market candle is silently unmet (no evaluation, no alert), but still
  // carries its date, same as the warmup skip does.
  [Fact]
  public async Task EvaluateAsync_ExtendedSessionCandle_IsSilentlyNotMet()
  {
    var gate = new Mock<ITradingSessionGate>();
    gate
      .Setup(g => g.IsExtendedSession("NASDAQ", TimeFrame.Day, It.IsAny<DateTime>()))
      .Returns(true);

    var evaluator = CreateEvaluatorWithGate(gate.Object);
    SetupCandles();

    var outcome = await evaluator.EvaluateAsync(SmaCondition(), UserLocale.Default);

    Assert.False(outcome.Met);
    Assert.Null(outcome.Value);
    Assert.NotNull(outcome.CandleDate);
    _mediator.Verify(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  // The per-subscription opt-out (IgnoreExtendedSessions=false): extended candles alert
  // like any others - the gate never consulted for suppression.
  [Fact]
  public async Task EvaluateAsync_ExtendedSessionCandle_OptIn_EvaluatesNormally()
  {
    var gate = new Mock<ITradingSessionGate>();
    gate
      .Setup(g => g.IsExtendedSession("NASDAQ", TimeFrame.Day, It.IsAny<DateTime>()))
      .Returns(true);

    var evaluator = CreateEvaluatorWithGate(gate.Object);
    SetupCandles();
    SetupMediator(triggered: true);

    var outcome = await evaluator.EvaluateAsync(SmaCondition(), UserLocale.Default, ignoreExtendedSessions: false);

    Assert.True(outcome.Met);
    _mediator.Verify(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EvaluateAsync_RegularSessionCandle_EvaluatesNormally()
  {
    var gate = new Mock<ITradingSessionGate>();
    gate
      .Setup(g => g.IsExtendedSession(It.IsAny<string?>(), It.IsAny<TimeFrame>(), It.IsAny<DateTime>()))
      .Returns(false);

    var evaluator = CreateEvaluatorWithGate(gate.Object);
    SetupCandles();
    SetupMediator(triggered: true);

    var outcome = await evaluator.EvaluateAsync(SmaCondition(), UserLocale.Default);

    Assert.True(outcome.Met);
    gate.Verify(g => g.IsExtendedSession("NASDAQ", TimeFrame.Day, It.IsAny<DateTime>()), Times.Once);
  }

  private SimpleConditionEvaluator CreateEvaluatorWithGate(ITradingSessionGate gate)
  {
    var serializer = new DefaultSerializer(
      new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error },
      Log.Logger);

    return new SimpleConditionEvaluator(_mediator.Object, _candleRepository.Object, serializer, gate);
  }

  private static RuleCondition SmaCondition() =>
    new()
    {
      Id = Guid.NewGuid(),
      Indicator = Indicator.SMA,
      Ticker = "MSFT",
      ExchangeName = "NASDAQ",
      TimeFrame = TimeFrame.Day,
      TemplateData = "{\"Periods\":20,\"TimeFrame\":2,\"CandlePart\":\"Close\",\"Deviation\":0.0005}"
    };
}
