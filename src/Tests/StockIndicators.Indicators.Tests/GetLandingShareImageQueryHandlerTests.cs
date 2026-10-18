using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators.GetLandingShareImage;
using StockIndicators.Domain.Models.Indicators;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The landing page's preview picture: a real chart, a fixed subject, and a picture per
/// language. The subject comes from the deployment rather than from the request, because the
/// endpoint is anonymous and Cloudflare keeps its answers for a week.
/// </summary>
public class GetLandingShareImageQueryHandlerTests
{
  private readonly Mock<IChartImageRenderer> _rendererMock = new();

  private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];

  /// <summary>
  /// A cache of its own per test: LazyCache's parameterless constructor shares
  /// <c>MemoryCache.Default</c>, so one test's cached picture would cancel the render
  /// another test counts.
  /// </summary>
  private GetLandingShareImageQueryHandler CreateHandler(string landingIndicator = "EMA") =>
    new(
      new CachingService(new Lazy<ICacheProvider>(
        () => new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())))),
      _rendererMock.Object,
      Microsoft.Extensions.Options.Options.Create(new ShareImageOptions { LandingIndicator = landingIndicator }),
      Logger.None);

  private void FakeRenderer() =>
    _rendererMock
      .Setup(r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Png);

  [Fact]
  public void LandingIndicatorShowsARealChart()
  {
    // the whole point of the picture is that it is an actual example chart: fixture candles
    // and a candle the production handler triggers on. An indicator without a fixture renders
    // a synthetic series, which would be a decoration again - what this replaced
    Assert.True(Enum.TryParse<Indicator>(new ShareImageOptions().LandingIndicator, out var indicator));
    Assert.True(
      ExampleMarketData.TryGetWindow(indicator, out _),
      $"{indicator} has no example fixture, so the landing picture would be a synthetic chart");
  }

  [Fact]
  public async Task Handle_RendersTheBrandCardWithoutTheDuplicatedBrandLine()
  {
    FakeRenderer();

    var png = await CreateHandler().Handle(new GetLandingShareImageQuery(UserLocale.Russian), CancellationToken.None);

    Assert.Equal(Png, png);

    _rendererMock.Verify(
      r => r.RenderAsync(
        It.IsAny<ChartData>(),
        It.Is<UserLocale>(locale => locale.Language == UserLocale.Russian),
        It.Is<ChartRenderOptions>(options =>
          options!.Mode == ChartRenderOptions.CardMode &&
          options.Title == "Stock Indicators" &&
          // the headline is the brand itself: the small brand line over it would say it twice
          options.Eyebrow == null &&
          // the pitch is the localized one, not English
          options.Subtitle == LocalizationCatalog.Get(LocalizationKey.BotProfileShortDescription, UserLocale.Russian)),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task Handle_CachesOnePicturePerLanguage()
  {
    FakeRenderer();
    var handler = CreateHandler();

    await handler.Handle(new GetLandingShareImageQuery(UserLocale.Russian), CancellationToken.None);
    await handler.Handle(new GetLandingShareImageQuery(UserLocale.Russian), CancellationToken.None);
    await handler.Handle(new GetLandingShareImageQuery("zz"), CancellationToken.None);

    // "zz" normalizes to English, which is its own picture (the card's text is localized)
    _rendererMock.Verify(
      r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()),
      Times.Exactly(2));
  }

  [Fact]
  public async Task Handle_AnUnknownConfiguredIndicatorAnswersNoPicture()
  {
    FakeRenderer();

    var png = await CreateHandler("NotAnIndicator").Handle(new GetLandingShareImageQuery(UserLocale.English), CancellationToken.None);

    // the endpoint then redirects to the brand card; nothing is rendered, and a typo in the
    // config is not a request-time exception
    Assert.Null(png);
    _rendererMock.Verify(
      r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task Handle_DoesNotCacheAFailedRender()
  {
    _rendererMock
      .SetupSequence(r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((byte[]?)null)
      .ReturnsAsync(Png);

    var handler = CreateHandler();
    var query = new GetLandingShareImageQuery(UserLocale.English);

    Assert.Null(await handler.Handle(query, CancellationToken.None));
    Assert.Equal(Png, await handler.Handle(query, CancellationToken.None));
  }
}
