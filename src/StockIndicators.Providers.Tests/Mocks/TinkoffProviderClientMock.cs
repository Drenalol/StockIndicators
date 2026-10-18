using Moq;
using StockIndicators.Abstract;
using StockIndicators.Providers.Tinkoff;

namespace StockIndicators.Providers.Tests.Mocks;

public static class TinkoffProviderClientMock
{
  public static Mock<IInstrumentSource> Create()
  {
    var mock = new Mock<IInstrumentSource>();
    
    mock
      .Setup(s => s.GetInstrumentsAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(() =>
        {
          var apple = new TinkoffInstrument("AAPL", "NMS", null, "USD", "Apple Inc.", null, null, "Stock");
          return [apple];
        }
      );
    
    return mock;
  }
}
