using Moq;
using Serilog.Core;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Candles;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Domain.Models.Subscriptions;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class TriggerChartServiceTests
{
  private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

  private readonly Mock<ICandleRepository> _candleRepositoryMock = new();
  private readonly Mock<ISerializer> _serializerMock = new();
  private readonly Mock<IChartImageRenderer> _imageRendererMock = new();

  private ITriggerChartService CreateService() =>
    new TriggerChartService(_candleRepositoryMock.Object, _serializerMock.Object, _imageRendererMock.Object, Logger.None);

  // The alert image is produced by the injected renderer (headless browser over the
  // frontend's chart core); the service tests fake its bytes, the PNG signature only
  // proves the plumbing passes the rendered image through
  private void FakeImageRenderer() =>
    _imageRendererMock
      .Setup(r => r.RenderAsync(
        It.IsAny<ChartData>(),
        It.IsAny<UserLocale>(),
        It.IsAny<ChartRenderOptions>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 });

  private static Candle[] CreateCandles(int count)
  {
    var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    return Enumerable.Range(0, count)
      .Select(i =>
      {
        var open = 100m + i * 0.1m;
        var close = 100m + (i + 1) * 0.1m;
        return new Candle("TEST", startDate.AddDays(i), TimeFrame.Day, open, close + 0.2m, open - 0.2m, close, 1000);
      })
      .ToArray();
  }

  // A subscription is a pure rule container: it carries no indicator/template/instrument,
  // so the builders stay minimal and every chart is driven by a TriggerChartContext.
  private static Subscription CreateSubscription() =>
    new()
    {
      Id = Guid.NewGuid(),
      UserId = Guid.NewGuid(),
      Status = SubscriptionStatus.Active,
      Cooldown = TimeSpan.FromMinutes(1)
    };

  private static TriggerChartContext Context(AppIndicator indicator, string? templateData = null, string? ticker = "TEST", string? exchangeName = null) =>
    new(indicator, templateData, ticker, exchangeName);

  [Fact]
  public async Task RenderPngAsync_ReturnsNullWhenFewerThanTwoCandles()
  {
    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([CreateCandles(1)[0]]);

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 2 });

    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, Context(AppIndicator.SMA, "{}"), UserLocale.Default);

    Assert.Null(result);
  }

  [Fact]
  public async Task RenderPngAsync_ReturnsPngWhenCandlesAreAvailable()
  {
    FakeImageRenderer();

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateCandles(80));

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 });

    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, Context(AppIndicator.SMA, "{}"), UserLocale.Default);

    Assert.NotNull(result);
    Assert.True(result!.Length > 0);
    Assert.Equal(PngSignature, result.Take(4));
  }

  [Fact]
  public async Task RenderPngAsync_PassesTheLocaleChartOptionsToTheImageRenderer()
  {
    FakeImageRenderer();

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateCandles(80));

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 });

    // The recipient turned the OHLC tooltip off: the alert image must be drawn without it
    var locale = new UserLocale(UserLocale.English, TimeZoneInfo.Utc, ShowOhlcTooltip: false);

    await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, Context(AppIndicator.SMA, "{}"), locale);

    _imageRendererMock.Verify(
      r => r.RenderAsync(It.IsAny<ChartData>(), locale, null, It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task RenderChartDataAsync_MapsDisplayStampedTimesToUnixSeconds()
  {
    var candles = CreateCandles(80);

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(candles);

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 });

    var data = await CreateService().RenderChartDataAsync(CreateSubscription(), candles[^1].Timestamp, Context(AppIndicator.SMA, "{}"), UserLocale.Default);

    Assert.NotNull(data);
    Assert.Equal(candles.Length, data!.Candles.Count);
    // display re-stamp is identity for UTC: the time maps to the candle date as if UTC
    var expected = new DateTimeOffset(DateTime.SpecifyKind(candles[0].Timestamp, DateTimeKind.Unspecified), TimeSpan.Zero).ToUnixTimeSeconds();
    Assert.Equal(expected, data.Candles[0].Time);
    // the trigger marker lands on the last candle and carries the localized label
    Assert.Equal(expected + (candles.Length - 1) * 86_400, data.Trigger!.Time);
    Assert.Equal("Trigger", data.Trigger.Label);
  }

  [Fact]
  public async Task RenderPngAsync_ResolvesTemplateFromConditionTemplateData()
  {
    FakeImageRenderer();

    var template = new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 };

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateCandles(80));

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(template);

    // The fired condition's TemplateData is the only template source the service has
    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, Context(AppIndicator.SMA, """{"Periods":10,"TimeFrame":2}"""), UserLocale.Default);

    Assert.NotNull(result);
    Assert.True(result!.Length > 0);
    Assert.Equal(PngSignature, result.Take(4));
    _serializerMock.Verify(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()), Times.Once);
  }

  [Fact]
  public async Task RenderPngAsync_FetchesEnoughCandlesToCoverIndicatorWarmup()
  {
    FakeImageRenderer();

    DateTime capturedStart = default;

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Callback<string, string?, DateTime, DateTime, TimeFrame, CancellationToken>((_, _, start, _, _, _) => capturedStart = start)
      .ReturnsAsync(CreateCandles(450));

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new EmaIndicatorTemplate { TimeFrame = TimeFrame.Minute, Periods = 150 });

    var triggeredAt = new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    var result = await CreateService().RenderPngAsync(CreateSubscription(), triggeredAt, Context(AppIndicator.EMA, """{"Periods":150,"TimeFrame":0}"""), UserLocale.Default);

    Assert.NotNull(result);
    Assert.Equal(triggeredAt - TimeSpan.FromMinutes(150 * 3 + 80), capturedStart);
  }

  [Fact]
  public async Task RenderPngAsync_UsesFallbackWindowForTemplatesWithoutCandleCount()
  {
    FakeImageRenderer();

    DateTime capturedStart = default;

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Callback<string, string?, DateTime, DateTime, TimeFrame, CancellationToken>((_, _, start, _, _, _) => capturedStart = start)
      .ReturnsAsync(CreateCandles(80));

    var triggeredAt = new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    // RoundPrice is the only template that does not declare a candle count (VWAP declares
    // Period), so it exercises the fixed fallback window; without TemplateData the
    // indicator defaults are used and the serializer is not consulted at all.
    var result = await CreateService().RenderPngAsync(CreateSubscription(), triggeredAt, Context(AppIndicator.RoundPrice), UserLocale.Default);

    Assert.NotNull(result);
    Assert.Equal(triggeredAt - TimeSpan.FromMinutes(80), capturedStart);
  }

  [Fact]
  public async Task RenderPngAsync_NullChartContext_ReturnsNullWithoutFetchingCandles()
  {
    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateCandles(80));

    // The subscription is a pure container: with no resolvable fired condition (the rule
    // tree was rebuilt after the alert) there is nothing left to draw — no fallback
    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, null, UserLocale.Default);

    Assert.Null(result);
    _candleRepositoryMock.Verify(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()), Times.Never);
    _imageRendererMock.Verify(r => r.RenderAsync(It.IsAny<ChartData>(), It.IsAny<UserLocale>(), It.IsAny<ChartRenderOptions>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task RenderPngAsync_ContextWithoutTicker_ReturnsNullWithoutFetchingCandles()
  {
    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateCandles(80));

    var context = Context(AppIndicator.SMA, "{}", ticker: null);

    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, context, UserLocale.Default);

    Assert.Null(result);
    _candleRepositoryMock.Verify(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public void TrimToIndicatorRange_DropsLeadingCandlesBeforeFirstSeriesPoint()
  {
    var candles = CreateCandles(80);
    var series = new List<ChartSeries>
    {
      new("EMA 10", ChartSeriesKind.Line, ChartPane.Price,
        [new ChartPoint(ChartX.ToX(candles[10].Timestamp), 105d), new ChartPoint(ChartX.ToX(candles[^1].Timestamp), 108d)],
        "#0ea5e9", [])
    };

    var trimmed = TriggerChartService.TrimToIndicatorRange(candles, series);

    Assert.Equal(candles[10..], trimmed);
  }

  [Fact]
  public void TrimToIndicatorRange_UsesEarliestPointAcrossAllSeries()
  {
    var candles = CreateCandles(80);
    var series = new List<ChartSeries>
    {
      new("Span B", ChartSeriesKind.Line, ChartPane.Price,
        [new ChartPoint(ChartX.ToX(candles[26].Timestamp), 100d)], "#ef4444", []),
      new("Tenkan", ChartSeriesKind.Line, ChartPane.Price,
        [new ChartPoint(ChartX.ToX(candles[9].Timestamp), 101d)], "#0ea5e9", [])
    };

    var trimmed = TriggerChartService.TrimToIndicatorRange(candles, series);

    Assert.Equal(candles[9..], trimmed);
  }

  [Fact]
  public void TrimToIndicatorRange_ReturnsAllCandlesWhenNoSeriesHasPoints()
  {
    var candles = CreateCandles(80);
    var series = new List<ChartSeries>
    {
      new("Level", ChartSeriesKind.Line, ChartPane.Price, [], "#64748b", [])
    };

    Assert.Same(candles, TriggerChartService.TrimToIndicatorRange(candles, series));
  }

  [Fact]
  public void TrimToIndicatorRange_DoesNotCutBelowMinCandles()
  {
    var candles = CreateCandles(66);
    var series = new List<ChartSeries>
    {
      new("SMA 50", ChartSeriesKind.Line, ChartPane.Price,
        [new ChartPoint(ChartX.ToX(candles[49].Timestamp), 105d), new ChartPoint(ChartX.ToX(candles[^1].Timestamp), 108d)],
        "#2563eb", [])
    };

    var trimmed = TriggerChartService.TrimToIndicatorRange(candles, series, 80);

    Assert.Equal(candles, trimmed);
  }

  [Fact]
  public void TrimToIndicatorRange_KeepsWarmupTrimWhenWindowIsDeep()
  {
    var candles = CreateCandles(130);
    var series = new List<ChartSeries>
    {
      new("SMA 50", ChartSeriesKind.Line, ChartPane.Price,
        [new ChartPoint(ChartX.ToX(candles[49].Timestamp), 105d)], "#2563eb", [])
    };

    var trimmed = TriggerChartService.TrimToIndicatorRange(candles, series, 80);

    Assert.Equal(candles[49..], trimmed);
  }

  [Fact]
  public void TrimToIndicatorRange_CapsVisibleCandlesAtMaxCandles()
  {
    var candles = CreateCandles(181);
    var series = new List<ChartSeries>
    {
      new("EMA 50", ChartSeriesKind.Line, ChartPane.Price,
        [new ChartPoint(ChartX.ToX(candles[100].Timestamp), 105d), new ChartPoint(ChartX.ToX(candles[^1].Timestamp), 108d)],
        "#0ea5e9", [])
    };

    var trimmed = TriggerChartService.TrimToIndicatorRange(candles, series, 80, 80);

    Assert.Equal(candles[101..], trimmed);
    Assert.Equal(candles[^1].Timestamp, trimmed[^1].Timestamp);
  }

  [Fact]
  public void TrimToIndicatorRange_CapsCandlesAtMaxCandlesWhenNoSeriesHasPoints()
  {
    var candles = CreateCandles(181);
    var series = new List<ChartSeries>
    {
      new("Level", ChartSeriesKind.Line, ChartPane.Price, [], "#64748b", [])
    };

    var trimmed = TriggerChartService.TrimToIndicatorRange(candles, series, 80, 80);

    Assert.Equal(candles[101..], trimmed);
  }

  [Fact]
  public void ResolveDisplayWindow_Week52SpansTheWholeCalendarLookback()
  {
    var window = TriggerChartService.ResolveDisplayWindow(new Week52HighLowIndicatorTemplate { Weeks = 52 });

    Assert.Equal(52 * 5 + 10, window);
  }

  [Fact]
  public void ResolveDisplayWindow_OtherIndicatorsKeepTheReadableWindow()
  {
    Assert.Equal(80, TriggerChartService.ResolveDisplayWindow(new RsiIndicatorTemplate()));
    Assert.Equal(80, TriggerChartService.ResolveDisplayWindow(new RoundPriceIndicatorTemplate()));
  }

  [Fact]
  public async Task RenderSnapshotDataAsync_Week52ShowsTheWholeLookbackNotTheReadableWindow()
  {
    // 500 consecutive daily bars: the Week52 coverage guard needs a full 364-day window,
    // and the snapshot fetch asks the cache for exactly warmup + readable window (500)
    var candles = CreateCandles(500);

    _candleRepositoryMock
      .Setup(r => r.GetLastCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(candles);

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new Week52HighLowIndicatorTemplate { TimeFrame = TimeFrame.Day, Weeks = 52 });

    var context = new SnapshotChartContext("TEST", null, TimeFrame.Day, [Context(AppIndicator.Week52HighLow, "{}")]);

    var data = await CreateService().RenderSnapshotDataAsync(CreateSubscription(), context, UserLocale.Default);

    Assert.NotNull(data);
    // the old 80-candle cap would cut the 52-week range down to its last quarter
    Assert.True(data!.Candles.Count > 80, $"visible candles: {data.Candles.Count}");
    Assert.True(data.Candles.Count <= 52 * 5 + 10, $"visible candles: {data.Candles.Count}");
    var expectedLast = new DateTimeOffset(DateTime.SpecifyKind(candles[^1].Timestamp, DateTimeKind.Unspecified), TimeSpan.Zero).ToUnixTimeSeconds();
    Assert.Equal(expectedLast, data.Candles[^1].Time);
  }

  [Fact]
  public void ClipSeriesToRange_DropsPointsBeforeFirstVisibleCandle()
  {
    var candles = CreateCandles(181);
    var series = new List<ChartSeries>
    {
      new("EMA 50", ChartSeriesKind.Line, ChartPane.Price,
        Enumerable.Range(0, 181).Select(i => new ChartPoint(ChartX.ToX(candles[i].Timestamp), 100d + i)).ToArray(),
        "#0ea5e9", []),
      new("Level", ChartSeriesKind.Line, ChartPane.Price, [], "#64748b", [])
    };

    var clipped = TriggerChartService.ClipSeriesToRange(series, candles[101].Timestamp);

    Assert.Equal(80, clipped[0].Points.Count);
    Assert.Equal(ChartX.ToX(candles[101].Timestamp), clipped[0].Points[0].X);
    Assert.Empty(clipped[1].Points);
    Assert.All(clipped[0].Points, p => Assert.True(p.X >= ChartX.ToX(candles[101].Timestamp)));
  }

  [Fact]
  public void BuildSpec_KeepsEveryCandleWhenWarmupTrimWouldLeaveASliver()
  {
    var candles = CreateCandles(66);
    var template = new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 50 };
    var built = IndicatorChartBuilder.Build(AppIndicator.SMA, template, candles);

    var spec = TriggerChartService.BuildSpec(AppIndicator.SMA, "TEST", TimeFrame.Day, UserLocale.Default, candles[^1].Timestamp, candles, built);

    Assert.Equal(66, spec.Candles.Count);
    Assert.Equal(candles[^1].Timestamp, spec.TriggerAt);
  }

  [Fact]
  public void BuildSpec_TrimsWarmupWhenFetchCoversWindowPlusWarmup()
  {
    var candles = CreateCandles(130);
    var template = new MaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 50 };
    var built = IndicatorChartBuilder.Build(AppIndicator.SMA, template, candles);

    var spec = TriggerChartService.BuildSpec(AppIndicator.SMA, "TEST", TimeFrame.Day, UserLocale.Default, candles[^1].Timestamp, candles, built);

    Assert.Equal(81, spec.Candles.Count);
    Assert.Equal(candles[^1].Timestamp, spec.TriggerAt);
  }

  [Fact]
  public async Task RenderPngAsync_TemplateNotDeserializable_FallsBackToIndicatorDefaults()
  {
    FakeImageRenderer();

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateCandles(80));

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns<object?>(null!);

    // A corrupt condition template body no longer kills the chart: the indicator's
    // default template (BollingerBands: Period 20) is drawn instead
    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, Context(AppIndicator.BollingerBands, "{}"), UserLocale.Default);

    Assert.NotNull(result);
    _serializerMock.Verify(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()), Times.Once);
  }

  [Fact]
  public void ToDisplayTimezone_ShiftsCandleDatesToUserWallClock()
  {
    var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    var candles = CreateCandles(3);
    var firstUtc = candles[0].Timestamp;

    var converted = TriggerChartService.ToDisplayTimezone(candles, timezone);

    Assert.NotSame(candles, converted);
    Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(firstUtc, timezone), converted[0].Timestamp);
    Assert.Equal(firstUtc, candles[0].Timestamp);
    Assert.Equal(candles[0].Close, converted[0].Close);
    Assert.Equal(candles[0].Updated, converted[0].Updated);
  }

  [Fact]
  public void ToDisplayTimezone_ReturnsTheSameArrayForUtc()
  {
    var candles = CreateCandles(3);

    Assert.Same(candles, TriggerChartService.ToDisplayTimezone(candles, TimeZoneInfo.Utc));
  }

  [Theory]
  [InlineData("Europe/Moscow", "2024-01-15T12:00:00Z", "UTC+3")]
  [InlineData("Asia/Kolkata", "2024-01-15T12:00:00Z", "UTC+5:30")]
  [InlineData("America/New_York", "2024-01-15T12:00:00Z", "UTC-5")]
  public void TimezoneSuffix_FormatsUtcOffset(string timezoneId, string triggeredAtUtc, string expected)
  {
    var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
    var triggeredAt = DateTime.Parse(triggeredAtUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    Assert.Equal(expected, TriggerChartService.TimezoneSuffix(timezone, triggeredAt));
  }

  [Fact]
  public void TimezoneSuffix_IsNullForUtc()
  {
    Assert.Null(TriggerChartService.TimezoneSuffix(TimeZoneInfo.Utc, new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)));
  }

  [Fact]
  public void BuildTitle_AppendsTimezoneSuffixForNonUtc()
  {
    var locale = new UserLocale(UserLocale.Russian, TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));
    var triggeredAt = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    Assert.Equal("TEST SMA Minute (UTC+3)", TriggerChartService.BuildTitle("TEST", AppIndicator.SMA, TimeFrame.Minute, locale, triggeredAt));
    Assert.Equal("TEST SMA Minute", TriggerChartService.BuildTitle("TEST", AppIndicator.SMA, TimeFrame.Minute, UserLocale.Default, triggeredAt));
  }

  [Fact]
  public async Task RenderPngAsync_ContextTickerAndExchange_DriveTheCandleFetch()
  {
    FakeImageRenderer();

    string? capturedTicker = null;
    string? capturedExchange = null;

    _candleRepositoryMock
      .Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()))
      .Callback<string, string?, DateTime, DateTime, TimeFrame, CancellationToken>((t, e, _, _, _, _) => { capturedTicker = t; capturedExchange = e; })
      .ReturnsAsync(CreateCandles(80));

    _serializerMock
      .Setup(s => s.DeserializeOrNull(It.IsAny<string>(), It.IsAny<Type>()))
      .Returns(new EmaIndicatorTemplate { TimeFrame = TimeFrame.Day, Periods = 10 });

    // A cross-instrument rule condition fired: the chart fetches the condition's instrument
    var context = Context(AppIndicator.EMA, """{"Periods":10,"TimeFrame":2}""", "BTC", "CCC");

    var result = await CreateService().RenderPngAsync(CreateSubscription(), DateTime.UtcNow, context, UserLocale.Default);

    Assert.NotNull(result);
    Assert.Equal("BTC", capturedTicker);
    Assert.Equal("CCC", capturedExchange);
  }

  [Fact]
  public void BuildSpec_MarksLastCandleInUserTimezone_WithLocalizedLabel()
  {
    var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    var locale = new UserLocale(UserLocale.Russian, timezone);
    var utcCandles = CreateCandles(5);
    var displayCandles = TriggerChartService.ToDisplayTimezone(utcCandles, timezone);
    var built = new IndicatorChartBuilder.Result([], false, null, null);
    var triggeredAt = new DateTime(2024, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    var spec = TriggerChartService.BuildSpec(AppIndicator.SMA, "TEST", TimeFrame.Day, locale, triggeredAt, displayCandles, built);

    Assert.Equal(displayCandles[^1].Timestamp, spec.TriggerAt);
    Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(utcCandles[^1].Timestamp, timezone), spec.TriggerAt);
    Assert.Equal("Триггер", spec.TriggerLabel);
    Assert.Equal("TEST SMA Day (UTC+3)", spec.Title);
  }

  [Fact]
  public void BuildSpec_UsesEnglishLabelForUtcLocale()
  {
    var utcCandles = CreateCandles(5);
    var built = new IndicatorChartBuilder.Result([], false, null, null);
    var triggeredAt = new DateTime(2024, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    var spec = TriggerChartService.BuildSpec(AppIndicator.SMA, "TEST", TimeFrame.Day, UserLocale.Default, triggeredAt, utcCandles, built);

    Assert.Equal(utcCandles[^1].Timestamp, spec.TriggerAt);
    Assert.Equal("Trigger", spec.TriggerLabel);
  }
}
