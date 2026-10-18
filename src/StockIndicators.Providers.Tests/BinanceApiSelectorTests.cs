using System.Net;
using Moq;
using Serilog;
using Serilog.Core;
using StockIndicators.Providers.Binance.Api;
using StockIndicators.Providers.Binance.Internal;
using StockIndicators.Providers.Binance.Schema;
using StockIndicators.Providers.Tests.Mocks;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class BinanceApiSelectorTests
{
  private readonly Mock<IBinanceApi> _primary = new();
  private readonly Mock<IBinanceApi> _mirror = new();

  public BinanceApiSelectorTests()
  {
    _primary.Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new BinanceExchangeInfo());
    _mirror.Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new BinanceExchangeInfo());
  }

  [Fact]
  public async Task Primary_IsUsed_ByDefault()
  {
    var selector = CreateSelector();

    await selector.ExecuteAsync((api, ct) => api.GetExchangeInfoAsync(ct), default);

    Assert.False(selector.UsesMirror);
    _primary.Verify(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()), Times.Once);
    _mirror.Verify(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Theory]
  [InlineData(HttpStatusCode.UnavailableForLegalReasons)]
  [InlineData(HttpStatusCode.Forbidden)]
  public async Task GeoBlocked_SwitchesToMirror_Once(HttpStatusCode statusCode)
  {
    var exception = await ThrottlingTestHelper.ThrottledAsync(statusCode);
    _primary
      .Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(exception);

    var selector = CreateSelector();

    await selector.ExecuteAsync((api, ct) => api.GetExchangeInfoAsync(ct), default);
    await selector.ExecuteAsync((api, ct) => api.GetExchangeInfoAsync(ct), default);

    Assert.True(selector.UsesMirror);
    _primary.Verify(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()), Times.Once);
    _mirror.Verify(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public async Task NonGeoError_StaysOnPrimary()
  {
    var exception = await ThrottlingTestHelper.ThrottledAsync(HttpStatusCode.InternalServerError);
    _primary
      .Setup(a => a.GetExchangeInfoAsync(It.IsAny<CancellationToken>()))
      .ThrowsAsync(exception);

    var selector = CreateSelector();

    await Assert.ThrowsAsync<Refit.ApiException>(
      () => selector.ExecuteAsync((api, ct) => api.GetExchangeInfoAsync(ct), default));

    Assert.False(selector.UsesMirror);
  }

  private BinanceApiSelector CreateSelector() => new(new BinanceApiClients(_primary.Object, _mirror.Object), Logger.None);
}
