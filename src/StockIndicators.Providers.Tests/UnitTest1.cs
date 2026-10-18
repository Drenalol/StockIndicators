using System.Globalization;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Serilog.Core;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.EventBus;
using StockIndicators.Application.Subscriptions.CheckSubscriptions;
using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Domain.Candles;
using Xunit;
using Xunit.Abstractions;

namespace StockIndicators.Providers.Tests;

public class UnitTest1
{
  private readonly ITestOutputHelper _testOutputHelper;
  private readonly IMediator _mediatorMock;

  public UnitTest1(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    _mediatorMock = MockMediator(testOutputHelper);
  }

  [Fact]
  public async Task TestAsync()
  {
    var eventBusMock = new Mock<IEventBus>();
    var scopeFactoryMock = new Mock<IServiceScopeFactory>();
    var buffer = new CandleProvider(
      Options.Create(new CandleBufferOptions { BatchTriggerDelaySec = 5 }),
      eventBusMock.Object,
      scopeFactoryMock.Object,
      Mock.Of<StockIndicators.Application.Charts.IChartLiveNotifier>(),
      Logger.None);

    for (var i = 0; i < 10500; i++)
      await buffer.PushAsync(MockCandle);

    await Task.Delay(6000);

    for (var i = 0; i < 2000; i++)
      await buffer.PushAsync(MockCandle);

    await Task.Delay(6000);

    buffer.Dispose();
  }

  [Fact(Skip = "Resolves scoped services from the root provider (broken DI); re-enable after creating a scope")]
  public async Task CacheTest()
  {
    var services = AdapterReadyWebApplication.Instance.Value.Services;
    var mediator = services.GetRequiredService<IMediator>();
    await mediator.Publish(new CheckSubscriptionsCommand([new InstrumentKey(MockCandle.Ticker, null)]));
    await Task.Delay(-1);
  }
    
  [Fact(Skip = "Resolves scoped ISubscriptionRepository from the root provider (broken DI); re-enable after creating a scope")]
  public async Task GetRuleSubsByInstrumentsTest()
  {
    var services = AdapterReadyWebApplication.Instance.Value.Services;
    var repository = services.GetRequiredService<ISubscriptionRepository>();
    var subscriptions = await repository.GetRuleSubscriptionsByInstrumentsAsync([new InstrumentKey(MockCandle.Ticker, null)], default);
    await Task.Delay(-1);
  }

  [Fact]
  public void ReduceTest()
  {
    var candles = new List<Candle>
    {
      new("AAPL", new DateTime(2022, 1, 1, 1, 1, 6), TimeFrame.Minute, 1,2,3,4,14),
      new("AAPL", new DateTime(2022, 1, 1, 1, 1, 4), TimeFrame.Minute, 1,2,3,4,5),
      new("AAPL", new DateTime(2022, 1, 1, 1, 1, 7), TimeFrame.Minute, 1,2,3,4,15),
      new("AAPL", new DateTime(2022, 1, 1, 1, 1, 2), TimeFrame.Minute, 1,2,3,4,5),
      new("AAPL", new DateTime(2022, 1, 1, 1, 1, 5), TimeFrame.Minute, 1,2,3,4,13)
    };

    var reducedCandles = CandleReducer.Reduce(candles);
    var reducedCandle = Assert.Single(reducedCandles);
    Assert.Equal(7, reducedCandle.Timestamp.Second);
    Assert.Equal(15, reducedCandle.Volume);
  }

  [Fact]
  public void IndicatorTest() => Assert.All(Enum.GetValues<Indicator>(), indicator => indicator.GetTemplate());

  [Fact]
  public void TimeFrameTest() => Assert.All(Enum.GetValues<TimeFrame>(), timeFrame => timeFrame.GetMaxPeriod());

  private static IMediator MockMediator(ITestOutputHelper? logger = null)
  {
    var mock = new Mock<IMediator>();
    mock.Setup(mediator => mediator.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
      .Returns(
        (ICommand request, CancellationToken _) =>
        {
          logger?.WriteLine($"{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)} Send called by {request.GetType().Name}");
          return Task.FromResult(Unit.Value);
        }
      );
    return mock.Object;
  }

  private static readonly Candle MockCandle = new(
    "AAPL",
    DateTime.UtcNow,
    TimeFrame.Minute,
    120,
    125,
    119,
    121,
    5555333
  );
}
