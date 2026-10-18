using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Charts;

public class TriggerChartService : ITriggerChartService
{
  /// <summary>Readable candle window kept on the trigger chart: the fetch window on top of
  /// the indicator warmup, and the floor the warmup trim may not cut below. Tunable.</summary>
  private const int CandleWindow = 80;

  /// <summary>Hard cap for the candle fetch so a stalled cache/provider can never hang the chart request.</summary>
  private static readonly TimeSpan CandleFetchTimeout = TimeSpan.FromSeconds(20);

  private readonly ICandleRepository _candleRepository;
  private readonly ISerializer _serializer;
  private readonly IChartImageRenderer _chartImageRenderer;
  private readonly ITradingSessionGate? _tradingSessionGate;
  private readonly ILogger _logger;

  public TriggerChartService(
    ICandleRepository candleRepository,
    ISerializer serializer,
    IChartImageRenderer chartImageRenderer,
    ILogger logger,
    ITradingSessionGate? tradingSessionGate = null
  )
  {
    _candleRepository = candleRepository;
    _serializer = serializer;
    _chartImageRenderer = chartImageRenderer;
    _tradingSessionGate = tradingSessionGate;
    _logger = logger;
  }

  public async Task<byte[]?> RenderPngAsync(
    Subscription subscription,
    DateTime triggeredAt,
    TriggerChartContext? chartContext,
    UserLocale locale,
    CancellationToken cancellationToken = default)
  {
    var data = await RenderChartDataAsync(subscription, triggeredAt, chartContext, locale, cancellationToken);

    // The alert picture is the app's own chart core rasterized headless, so it carries the
    // recipient's chart options and can never look different from the chart in the app
    return data is null ? null : await _chartImageRenderer.RenderAsync(data, locale, null, cancellationToken);
  }

  public async Task<ChartData?> RenderChartDataAsync(
    Subscription subscription,
    DateTime triggeredAt,
    TriggerChartContext? chartContext,
    UserLocale locale,
    CancellationToken cancellationToken = default)
  {
    var spec = await BuildSpecAsync(subscription, triggeredAt, chartContext, locale, cancellationToken);

    return spec is null ? null : ChartDataMapper.ToChartData(spec);
  }

  private async Task<ChartSpec?> BuildSpecAsync(
    Subscription subscription,
    DateTime triggeredAt,
    TriggerChartContext? chartContext,
    UserLocale locale,
    CancellationToken cancellationToken = default)
  {
    // The chart always belongs to the rule condition that fired; a subscription has no
    // indicator/instrument of its own, so without a resolvable condition there is nothing
    // to draw. The callers that render a stored trigger log their own reason (resolver).
    if (chartContext is null)
    {
      _logger.Debug(
        "Trigger chart skipped for {SubscriptionId}: the alert records no fired condition",
        subscription.Id.ToString()
      );
      return null;
    }

    if (string.IsNullOrWhiteSpace(chartContext.Ticker))
    {
      _logger.Warning(
        "Trigger chart skipped for {SubscriptionId}: the fired condition names no instrument",
        subscription.Id.ToString()
      );
      return null;
    }

    var indicator = chartContext.Indicator;
    var template = ResolveTemplate(chartContext);

    var timeFrame = template is ITimeFrameDependentIndicatorTemplate timeFrameTemplate
      ? timeFrameTemplate.TimeFrame
      : TimeFrame.Minute;

    // A rule condition may target a different instrument than any other condition of the
    // rule; the chart shows the candles of the instrument that actually fired
    var ticker = chartContext.Ticker;
    var exchangeName = chartContext.ExchangeName;

    var start = triggeredAt - (timeFrame.GetCandlePeriod() * GetCandleCount(template));
    var end = triggeredAt;

    var candles = await FetchCandlesAsync(ticker, exchangeName, start, end, timeFrame, subscription.Id, cancellationToken);

    if (candles is null)
      return null;

    if (candles.Length < 2)
    {
      _logger.Warning(
        "Trigger chart unavailable for {SubscriptionId} ({Ticker}): not enough candles in cache ({Count}) around {TriggeredAt}",
        subscription.Id.ToString(),
        ticker,
        candles.Length,
        triggeredAt
      );
      return null;
    }

    // Display-only re-stamp: cached candles stay UTC, but the axis labels and series X
    // coordinates are shifted to the user's wall clock so the chart shows their timezone.
    var displayCandles = ToDisplayTimezone(candles, locale.Timezone);

    var built = IndicatorChartBuilder.Build(indicator, template, displayCandles);

    // The marker direction: the handler's own comparison math replayed on the computed
    // series, so the arrow on the picture matches the signal the alert announced.
    var direction = TriggerDirectionResolver.Resolve(
      indicator, template, displayCandles, built.Series, displayCandles[^1].Timestamp);

    return BuildSpec(indicator, ticker, timeFrame, locale, triggeredAt, displayCandles, built, direction);
  }

  /// <summary>
  /// Fetches the candle window with the hard timeout cap; null when the fetch times out
  /// (a stalled cache/provider must never hang a chart request). Used by the trigger chart,
  /// whose window is anchored on the trigger moment.
  /// </summary>
  private async Task<Candle[]?> FetchCandlesAsync(
    string ticker,
    string? exchangeName,
    DateTime start,
    DateTime end,
    TimeFrame timeFrame,
    Guid subscriptionId,
    CancellationToken cancellationToken
  )
  {
    using var candleFetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    candleFetchCts.CancelAfter(CandleFetchTimeout);

    try
    {
      return await _candleRepository
        .GetCandlesAsync(ticker, exchangeName, start, end, timeFrame, candleFetchCts.Token);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      _logger.Warning(
        "Candle fetch for {SubscriptionId} ({Ticker}) timed out after {TimeoutSeconds}s around {End}",
        subscriptionId.ToString(),
        ticker,
        CandleFetchTimeout.TotalSeconds,
        end
      );

      return null;
    }
  }

  /// <summary>
  /// The newest candles the cache holds for the instrument (the provider is consulted only
  /// for holes inside the stored dictionary), capped by the same hard timeout. Used by the
  /// snapshot chart, whose window ends on the freshest candle rather than the wall clock.
  /// </summary>
  private async Task<Candle[]?> FetchLastCandlesAsync(
    string ticker,
    string? exchangeName,
    int periods,
    TimeFrame timeFrame,
    Guid subscriptionId,
    CancellationToken cancellationToken
  )
  {
    using var candleFetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    candleFetchCts.CancelAfter(CandleFetchTimeout);

    try
    {
      return await _candleRepository
        .GetLastCandlesAsync(ticker, exchangeName, periods, timeFrame, candleFetchCts.Token);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      _logger.Warning(
        "Last-candle fetch for {SubscriptionId} ({Ticker}) timed out after {TimeoutSeconds}s",
        subscriptionId.ToString(),
        ticker,
        CandleFetchTimeout.TotalSeconds
      );

      return null;
    }
  }

  public async Task<DateTime?> ResolveSnapshotAnchorAsync(
    Subscription subscription,
    SnapshotChartContext context,
    CancellationToken cancellationToken = default
  )
  {
    if (string.IsNullOrWhiteSpace(context.Ticker))
      return null;

    // Only the newest candle is needed for the freshness signal; the date is UTC (not the
    // display re-stamp), so the client can compare it with its own clock
    var candles = await FetchLastCandlesAsync(context.Ticker, context.ExchangeName, 1, context.TimeFrame, subscription.Id, cancellationToken);

    return candles is { Length: > 0 } ? candles[^1].Timestamp : null;
  }

  public async Task<ChartData?> RenderSnapshotDataAsync(
    Subscription subscription,
    SnapshotChartContext context,
    UserLocale locale,
    CancellationToken cancellationToken = default
  )
  {
    var spec = await BuildSnapshotSpecAsync(subscription, context, locale, cancellationToken);

    return spec is null ? null : ChartDataMapper.ToChartData(spec);
  }

  /// <summary>
  /// Snapshot pipeline: fetch the group's candle window, compute and merge every
  /// indicator of the group, trim and attach the trigger marker.
  /// </summary>
  private async Task<ChartSpec?> BuildSnapshotSpecAsync(
    Subscription subscription,
    SnapshotChartContext context,
    UserLocale locale,
    CancellationToken cancellationToken
  )
  {
    if (context.Indicators.Count == 0 || string.IsNullOrWhiteSpace(context.Ticker))
      return null;

    // The window must fit the hungriest indicator of the group (an EMA(150) needs 450
    // warmup candles): fetch once per group at the maximum, and compute every indicator
    // of the group on the same candles
    var templates = new List<IIndicatorTemplate>(context.Indicators.Count);
    var candleCount = CandleWindow;
    var displayWindow = CandleWindow;

    foreach (var indicator in context.Indicators)
    {
      var template = ResolveTemplate(indicator);
      templates.Add(template);
      candleCount = Math.Max(candleCount, GetCandleCount(template));
      displayWindow = Math.Max(displayWindow, ResolveDisplayWindow(template));
    }

    // The snapshot window ends on the newest candle the cache holds, not on the wall clock:
    // the candle repository only backfills holes inside the stored dictionary, never the
    // fresh tail after its last candle (that one only ever arrives as live provider pushes),
    // so while the market is closed or the provider has not streamed yet a [now - N, now]
    // range comes out empty. Showing the last known state beats showing nothing.
    var candles = await FetchLastCandlesAsync(context.Ticker, context.ExchangeName, candleCount, context.TimeFrame, subscription.Id, cancellationToken);

    if (candles is null || candles.Length < 2)
    {
      _logger.Warning(
        "Snapshot chart unavailable for {SubscriptionId} ({Ticker} {TimeFrame}): not enough candles in cache ({Count})",
        subscription.Id.ToString(),
        context.Ticker,
        context.TimeFrame,
        candles?.Length ?? 0
      );

      return null;
    }

    // Daily charts get the TradingView-style extended treatment: extended minute bars
    // merge into the live forming bar of the day, and the newest of them rides along as
    // the labeled quote line (the exchange's own daily bars never carry extended prices)
    ExtendedQuoteData? extendedQuote = null;

    if (context.TimeFrame == TimeFrame.Day)
    {
      var resolution = await ExtendedQuoteResolver.ResolveAsync(
        _candleRepository, _tradingSessionGate, context.Ticker, context.ExchangeName, candles, locale, cancellationToken);

      candles = resolution.Candles;
      extendedQuote = resolution.Quote;
    }

    // Display-only re-stamp: cached candles stay UTC, but the axis labels and series X
    // coordinates are shifted to the user's wall clock so the chart shows their timezone.
    var displayCandles = ToDisplayTimezone(candles, locale.Timezone);

    var results = new List<IndicatorChartBuilder.Result>(context.Indicators.Count);

    for (var i = 0; i < context.Indicators.Count; i++)
      results.Add(IndicatorChartBuilder.Build(context.Indicators[i].Indicator, templates[i], displayCandles));

    var merged = IndicatorChartBuilder.Merge(results);
    var visibleCandles = TrimToIndicatorRange(displayCandles, merged.Series, CandleWindow, displayWindow);
    var visibleSeries = ClipSeriesToRange(merged.Series, visibleCandles[0].Timestamp);

    var spec = new ChartSpec(
      Title: BuildSnapshotTitle(context.Ticker, context.TimeFrame, locale, DateTime.UtcNow),
      Candles: ToChartCandles(visibleCandles),
      Series: visibleSeries,
      HasOscillator: merged.HasOscillator,
      OscMin: merged.OscMin,
      OscMax: merged.OscMax,
      // The freshness anchor stays UTC (the raw fetched candle, not the display re-stamp)
      LastCandleAt: DateTime.SpecifyKind(candles[^1].Timestamp, DateTimeKind.Utc),
      Extended: extendedQuote,
      Session: _tradingSessionGate?.CurrentMarketSession(context.ExchangeName, DateTime.UtcNow)
    );

    // The "Trigger" marker is drawn only while the subscription is in the triggered phase.
    // The marker X must be re-stamped into the user's wall clock exactly like the candles
    // (the trigger chart does this via the last visible candle's date), and the marker is
    // dropped when the trigger moment has left the visible window.
    if (context.TriggerAt is { } triggeredAt)
    {
      var displayTrigger = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(triggeredAt, DateTimeKind.Utc), locale.Timezone);

      if (displayTrigger >= visibleCandles[0].Timestamp && displayTrigger <= visibleCandles[^1].Timestamp)
      {
        // The snapshot does not know which condition of the group fired (the group chart
        // marker shows the subscription's trigger phase): resolve every indicator on the
        // merged series and take the decision when it is unique - one buy-side and one
        // sell-side signal is genuinely ambiguous and stays undirected.
        spec = spec with
        {
          TriggerAt = displayTrigger,
          TriggerLabel = LocalizationCatalog.Get(LocalizationKey.ChartTrigger, locale.Language),
          TriggerDirection = ResolveSnapshotDirection(context, templates, displayCandles, merged.Series, displayTrigger)
        };
      }
    }

    return spec;
  }

  /// <summary>
  /// Merges the per-indicator direction decisions of a snapshot group: a unique bullish or
  /// bearish decision wins, a group of only directionless indicators is neutral, and both
  /// a buy-side and a sell-side signal at the same candle stays undirected (geometry decides).
  /// </summary>
  private static ChartTriggerDirection? ResolveSnapshotDirection(
    SnapshotChartContext context,
    IReadOnlyList<IIndicatorTemplate> templates,
    Candle[] displayCandles,
    IReadOnlyList<ChartSeries> series,
    DateTime displayTrigger)
  {
    ChartTriggerDirection? resolved = null;

    for (var i = 0; i < context.Indicators.Count; i++)
    {
      var direction = TriggerDirectionResolver.Resolve(
        context.Indicators[i].Indicator, templates[i], displayCandles, series, displayTrigger);

      if (direction is null || direction == ChartTriggerDirection.Neutral)
      {
        if (direction == ChartTriggerDirection.Neutral && resolved is null)
          resolved = direction;

        continue;
      }

      if (resolved is null or ChartTriggerDirection.Neutral)
      {
        resolved = direction;
        continue;
      }

      if (resolved != direction)
        return null;
    }

    return resolved;
  }

  internal static string BuildSnapshotTitle(string ticker, TimeFrame timeFrame, UserLocale locale, DateTime endUtc)
  {
    var baseTitle = $"{ticker} {timeFrame}";
    var suffix = TimezoneSuffix(locale.Timezone, endUtc);

    return suffix is null ? baseTitle : $"{baseTitle} ({suffix})";
  }

  /// <summary>
  /// Assembles the render spec from candles already fetched and re-stamped to the user's
  /// timezone. The window always ends on the trigger moment, so the marker is drawn on the
  /// last visible candle (its re-stamped date keeps the X coordinate aligned with the candle).
  /// </summary>
  internal static ChartSpec BuildSpec(
    Indicator indicator,
    string ticker,
    TimeFrame timeFrame,
    UserLocale locale,
    DateTime triggeredAt,
    Candle[] displayCandles,
    IndicatorChartBuilder.Result built,
    ChartTriggerDirection? triggerDirection = null)
  {
    // No cap here: the trigger chart fetch is already bounded to warmup + window, so the
    // visible range lands at ~80 candles naturally (plus the warmup boundary candle).
    var visibleCandles = TrimToIndicatorRange(displayCandles, built.Series, CandleWindow);

    return new ChartSpec(
      Title: BuildTitle(ticker, indicator, timeFrame, locale, triggeredAt),
      Candles: ToChartCandles(visibleCandles),
      Series: built.Series,
      HasOscillator: built.HasOscillator,
      OscMin: built.OscMin,
      OscMax: built.OscMax,
      TriggerAt: visibleCandles[^1].Timestamp,
      TriggerLabel: LocalizationCatalog.Get(LocalizationKey.ChartTrigger, locale.Language),
      TriggerDirection: triggerDirection
    );
  }

  /// <summary>
  /// Re-stamps candle dates into the user's wall clock for display. The cached arrays are not
  /// mutated: library indicator math is index-based, so only the chart X coordinates shift.
  /// </summary>
  internal static Candle[] ToDisplayTimezone(Candle[] candles, TimeZoneInfo timezone)
  {
    if (timezone == TimeZoneInfo.Utc)
      return candles;

    var converted = new Candle[candles.Length];

    for (var i = 0; i < candles.Length; i++)
    {
      var candle = candles[i];
      var localDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(candle.Timestamp, DateTimeKind.Utc), timezone);

      converted[i] = new Candle(
        candle.Ticker,
        localDate,
        candle.TimeFrame,
        candle.Open,
        candle.High,
        candle.Low,
        candle.Close,
        candle.Volume,
        candle.IsNotEmpty,
        candle.IsCompleted,
        candle.ExchangeName
      ) { Updated = candle.Updated };
    }

    return converted;
  }

  /// <summary>
  /// How many candles to fetch before the trigger: the indicator warmup plus the readable
  /// window on top of it, so after the warmup trim the chart still spans a full window and
  /// the overlay series has values across the whole visible range (an EMA(150) needs 450
  /// warmup candles plus the 80-candle window, or the series comes out entirely null).
  /// </summary>
  private static int GetCandleCount(IIndicatorTemplate template)
  {
    if (template is ICandleCountDependentIndicatorTemplate candleCountDependent)
      return CandleWindow + candleCountDependent.GetCandleCount();

    return CandleWindow;
  }

  /// <summary>Bars added on top of the Week52 calendar span: closed-day gaps stretch the
  /// bar count of the same calendar window past weeks*5.</summary>
  private const int Week52DisplaySlack = 10;

  /// <summary>
  /// The snapshot's visible-candle cap per template: the 52-week extremes are only
  /// meaningful with the whole calendar lookback on screen (the 80-candle readable window
  /// would show the last quarter of the range), so a Week52 chart spans weeks*5 daily
  /// bars plus slack for provider gaps; every other indicator keeps the readable window.
  /// The fetch is unaffected - it already covers the warmup either way.
  /// </summary>
  internal static int ResolveDisplayWindow(IIndicatorTemplate template) =>
    template is Week52HighLowIndicatorTemplate week52
      ? week52.Weeks * 5 + Week52DisplaySlack
      : CandleWindow;

  /// <summary>
  /// Resolves the indicator template of the fired rule condition; a missing or corrupt
  /// body falls back to the indicator's default template.
  /// </summary>
  private IIndicatorTemplate ResolveTemplate(TriggerChartContext chartContext)
  {
    if (string.IsNullOrWhiteSpace(chartContext.TemplateData))
      return (IIndicatorTemplate)Activator.CreateInstance(chartContext.Indicator.GetTemplate())!;

    if (_serializer.DeserializeOrNull(chartContext.TemplateData, chartContext.Indicator.GetTemplate()) is IIndicatorTemplate contextTemplate)
      return contextTemplate;

    _logger.Warning(
      "Trigger chart condition template is not deserializable ({Indicator}); falling back to the indicator defaults",
      chartContext.Indicator
    );

    return (IIndicatorTemplate)Activator.CreateInstance(chartContext.Indicator.GetTemplate())!;
  }

  internal static string BuildTitle(string ticker, Indicator indicator, TimeFrame timeFrame, UserLocale locale, DateTime triggeredAtUtc)
  {
    var baseTitle = $"{ticker} {indicator} {timeFrame}";
    var suffix = TimezoneSuffix(locale.Timezone, triggeredAtUtc);

    return suffix is null ? baseTitle : $"{baseTitle} ({suffix})";
  }

  /// <summary>
  /// The UTC offset of the user's timezone at the trigger moment (e.g. "UTC+3", "UTC+5:30"),
  /// or null for UTC itself (the default, not worth showing). The offset — unlike a zone
  /// abbreviation — is unambiguous across DST transitions and platforms.
  /// </summary>
  internal static string? TimezoneSuffix(TimeZoneInfo timezone, DateTime triggeredAtUtc)
  {
    if (timezone == TimeZoneInfo.Utc)
      return null;

    var offset = timezone.GetUtcOffset(DateTime.SpecifyKind(triggeredAtUtc, DateTimeKind.Utc));

    if (offset == TimeSpan.Zero)
      return "UTC";

    var sign = offset < TimeSpan.Zero ? "-" : "+";
    var absolute = offset.Duration();

    return absolute.Minutes == 0
      ? $"UTC{sign}{(int)absolute.TotalHours}"
      : $"UTC{sign}{(int)absolute.TotalHours}:{absolute.Minutes:D2}";
  }

  /// <summary>
  /// Drops the leading candles that no indicator series covers yet (moving-average warmup),
  /// so the chart spans the range the indicator has values for instead of a fixed window.
  /// The trim never cuts below <paramref name="minCandles" />: when the cache holds less
  /// history than the indicator needs, showing every candle beats showing a bare sliver.
  /// The visible range is capped at <paramref name="maxCandles" />: a long warmup fetch
  /// (EMA(50)+window on daily history) must never cram hundreds of sub-pixel candles into
  /// the picture, where the bodies collapse to wick thickness. The trigger chart is
  /// unaffected — it fetches exactly warmup + window, so both bounds land on the same candle.
  /// </summary>
  internal static Candle[] TrimToIndicatorRange(Candle[] candles, IReadOnlyList<ChartSeries> series, int minCandles = 0, int maxCandles = int.MaxValue)
  {
    double? firstPointX = null;

    foreach (var s in series)
      foreach (var point in s.Points)
        if (firstPointX is null || point.X < firstPointX)
          firstPointX = point.X;

    var floor = Math.Max(0, candles.Length - minCandles);

    // No series points at all: keep everything up to the cap (an indicator without overlay
    // points - Gap, RoundPrice - still has the candles themselves to show).
    var start = firstPointX is null ? 0 : candles.Length;

    if (firstPointX is not null)
      for (var i = 0; i < candles.Length; i++)
        if (ChartX.ToX(candles[i].Timestamp) >= firstPointX)
        {
          start = i;
          break;
        }

    start = Math.Min(start, floor);
    start = Math.Max(start, candles.Length - maxCandles);

    return start == 0 ? candles : candles[start..];
  }

  /// <summary>
  /// Drops indicator points that fall before the first visible candle: when the candle cap
  /// cuts the fetched window, leftover series points would stretch the X axis past the
  /// candles, leaving orphan lines on the left and keeping the sub-pixel candle pitch.
  /// </summary>
  internal static IReadOnlyList<ChartSeries> ClipSeriesToRange(IReadOnlyList<ChartSeries> series, DateTime from)
  {
    var fromX = ChartX.ToX(from);

    return series
      .Select(s =>
      {
        if (s.Points.Count == 0 || s.Points[0].X >= fromX)
          return s;

        var points = s.Points.Where(p => p.X >= fromX).ToArray();
        return points.Length == s.Points.Count ? s : s with { Points = points };
      })
      .ToArray();
  }

  internal static ChartCandle[] ToChartCandles(Candle[] candles)
  {
    var result = new ChartCandle[candles.Length];

    for (var i = 0; i < candles.Length; i++)
    {
      var candle = candles[i];
      result[i] = new ChartCandle(
        ChartX.ToX(candle.Timestamp),
        (double)candle.Open,
        (double)candle.High,
        (double)candle.Low,
        (double)candle.Close,
        (double)candle.Volume
      );
    }

    return result;
  }

}
