using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators.GetIndicatorShareImage;
using StockIndicators.Domain.Models.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The picture behind the shared link's preview: rendered once per indicator and language
/// and kept in process, while a failure is not kept (the next unfurl must try again instead
/// of serving the brand fallback until the cache expires).
/// </summary>
public class GetIndicatorShareImageQueryHandlerTests
{
  private readonly Mock<IChartImageRenderer> _rendererMock = new();

  private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

  /// <summary>
  /// A cache of its own per test: LazyCache's parameterless constructor shares
  /// <c>MemoryCache.Default</c>, and one test's cached picture would silently cancel the
  /// render another test is counting.
  /// </summary>
  private GetIndicatorShareImageQueryHandler CreateHandler() =>
    new(new CachingService(new Lazy<ICacheProvider>(
          () => new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())))), _rendererMock.Object, Logger.None);

  private void FakeRenderer() =>
    _rendererMock
      .Setup(r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Png);

  [Fact]
  public async Task Handle_RendersTheCardShapeInTheRequestedLanguage()
  {
    FakeRenderer();

    var png = await CreateHandler().Handle(new GetIndicatorShareImageQuery(Indicator.RSI, UserLocale.Russian), CancellationToken.None);

    Assert.Equal(Png, png);

    _rendererMock.Verify(
      r => r.RenderAsync(
        It.IsAny<ChartData>(),
        It.Is<UserLocale>(locale => locale.Language == UserLocale.Russian),
        It.Is<ChartRenderOptions>(options => options!.Mode == ChartRenderOptions.CardMode && options.Title == Indicator.RSI.ToString()),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task Handle_CachesThePicturePerIndicatorAndLanguage()
  {
    FakeRenderer();
    var handler = CreateHandler();

    await handler.Handle(new GetIndicatorShareImageQuery(Indicator.RSI, UserLocale.Russian), CancellationToken.None);
    await handler.Handle(new GetIndicatorShareImageQuery(Indicator.RSI, UserLocale.Russian), CancellationToken.None);
    await handler.Handle(new GetIndicatorShareImageQuery(Indicator.RSI, UserLocale.English), CancellationToken.None);

    // the burst of unfurls one shared link makes costs one browser run per language
    _rendererMock.Verify(
      r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()),
      Times.Exactly(2));
  }

  [Fact]
  public async Task Handle_DoesNotCacheAFailedRender()
  {
    _rendererMock
      .SetupSequence(r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((byte[]?)null)
      .ReturnsAsync(Png);

    var handler = CreateHandler();
    var query = new GetIndicatorShareImageQuery(Indicator.MACD, UserLocale.English);

    Assert.Null(await handler.Handle(query, CancellationToken.None));
    Assert.Equal(Png, await handler.Handle(query, CancellationToken.None));
  }

  [Fact]
  public async Task Handle_FallsBackToEnglishForAnUnsupportedLanguage()
  {
    FakeRenderer();

    // a messenger that asks for a language the app does not have must still get a picture:
    // the text localizes through the catalog's fallback, the render is not skipped
    var png = await CreateHandler().Handle(new GetIndicatorShareImageQuery(Indicator.SMA, "zz"), CancellationToken.None);

    Assert.Equal(Png, png);

    _rendererMock.Verify(
      r => r.RenderAsync(
        It.IsAny<ChartData>(),
        It.Is<UserLocale>(locale => locale.Language == UserLocale.English),
        It.IsAny<ChartRenderOptions?>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }
}
