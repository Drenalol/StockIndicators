using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Renders the indicator + template of the rule condition that actually fired the alert —
/// the subscription itself carries no indicator or instrument. Null ticker/exchange inside
/// the context are not possible in practice (rule conditions always carry them).
/// </summary>
public sealed record TriggerChartContext(
  Indicator Indicator,
  string? TemplateData,
  string? Ticker = null,
  string? ExchangeName = null
);

/// <summary>
/// Builds and renders a candlestick + indicator chart for a subscription trigger (recomputed on
/// demand from the candle cache). Dates are drawn in the user's timezone; the timezone
/// abbreviation is appended to the title when it is not UTC. Returns <c>null</c> when there is
/// not enough candle data or the alert records no condition to draw.
/// </summary>
public interface ITriggerChartService
{
  /// <summary>
  /// The trigger chart as a PNG for the alert channels (push/messenger images cannot render
  /// live chart code): the picture is the app's own chart core rasterized headless (see
  /// <see cref="IChartImageRenderer"/>), carrying the recipient's chart options. A null
  /// <paramref name="chartContext"/> (an alert collected without a met condition) leaves
  /// nothing to render — the result is null.
  /// </summary>
  Task<byte[]?> RenderPngAsync(
    Subscription subscription,
    DateTime triggeredAt,
    TriggerChartContext? chartContext,
    UserLocale locale,
    CancellationToken cancellationToken = default
  );

  /// <summary>
  /// The same trigger chart as raw series data (the chart endpoints): candles, overlays
  /// and the trigger marker as numbers the client plots itself. Null under the same
  /// conditions as <see cref="RenderPngAsync"/>.
  /// </summary>
  Task<ChartData?> RenderChartDataAsync(
    Subscription subscription,
    DateTime triggeredAt,
    TriggerChartContext? chartContext,
    UserLocale locale,
    CancellationToken cancellationToken = default
  );

  /// <summary>
  /// The snapshot chart of one group as raw series data (the chart endpoints), computed
  /// from the live candle cache on demand.
  /// </summary>
  Task<ChartData?> RenderSnapshotDataAsync(
    Subscription subscription,
    SnapshotChartContext context,
    UserLocale locale,
    CancellationToken cancellationToken = default
  );

  /// <summary>
  /// The UTC date of the newest candle the cache holds for a snapshot group: the stored
  /// snapshot rows carry it as the freshness signal (the client compares it with its own
  /// clock to tell the user it is showing the last known state over a closed exchange).
  /// Null when the cache window expired for the group — the row is then skipped, exactly
  /// like a group whose chart could not be rendered.
  /// </summary>
  Task<DateTime?> ResolveSnapshotAnchorAsync(
    Subscription subscription,
    SnapshotChartContext context,
    CancellationToken cancellationToken = default
  );
}

/// <summary>
/// One chart of the subscription snapshot: a (ticker, exchange, timeframe) group with every
/// distinct indicator condition of the group. <see cref="TriggerAt"/> is non-null only when
/// the subscription is in the triggered phase; the marker is drawn when it falls inside the
/// visible window.
/// </summary>
public sealed record SnapshotChartContext(
  string Ticker,
  string? ExchangeName,
  TimeFrame TimeFrame,
  IReadOnlyList<TriggerChartContext> Indicators,
  DateTime? TriggerAt = null
);
