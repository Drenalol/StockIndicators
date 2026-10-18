using StockIndicators.Application.Rules;

namespace StockIndicators.Application.Charts;

/// <summary>
/// The chart payload of the chart endpoints: raw series data the client plots with its
/// own chart core (zoom, pan, crosshair), and the source of the alert image. Times are
/// Unix seconds of the display re-stamped wall clock (the candles and series X are already
/// shifted to the user's timezone), so the client renders them as if UTC and the axis
/// shows the user's local time.
/// </summary>
public sealed record ChartData(
  string Title,
  IReadOnlyList<ChartCandleData> Candles,
  IReadOnlyList<ChartSeriesData> Series,
  double? OscMin,
  double? OscMax,
  ChartTriggerData? Trigger,
  // UTC date of the newest candle in the window (not the display re-stamp): the live
  // freshness anchor, moved by candle pushes together with the picture
  DateTime? LastCandleAt = null,
  // The extended-hours quote (pre/post-market), null outside extended sessions. The daily
  // bar itself never contains extended prices (exchange aggregation rules), so services
  // like Google Finance show this as a separate "Pre-market/After-hours" line too
  ExtendedQuoteData? Extended = null,
  // The exchange's market phase when the chart was built: "regular", "pre", "post",
  // "overnight" or "closed"; null for venues the session gate does not model (24/7 crypto).
  // While an extended session runs the client shows the Extended quote line instead of
  // any closed-market notice (the merged live bar keeps the chart fresh)
  string? Session = null
);

/// <summary>
/// The latest extended-hours trade with the regular close it is priced against.
/// <c>Session</c> is "pre", "post" or "overnight"; <c>At</c> is the UTC time of the
/// extended candle; <c>Label</c> is the localized session name for the chart's
/// price-scale tag (same per-user language as the trigger marker label).
/// </summary>
public sealed record ExtendedQuoteData(string Session, double Price, DateTime At, double? ReferenceClose, string Label);

/// <summary>A candlestick sample; <see cref="Time"/> is Unix seconds, Volume may be 0.</summary>
public sealed record ChartCandleData(long Time, double Open, double High, double Low, double Close, double Volume);

/// <summary>
/// An indicator overlay; <c>Kind</c> is "line"/"area"/"bar", <c>Pane</c> "price"/"oscillator".
/// <c>Dashed</c> asks for a thin dashed stroke (deviation-band boundary), <c>Hidden</c> keeps
/// the series out of legend/label chrome while still plotting it.
/// </summary>
public sealed record ChartSeriesData(
  string Name,
  string Kind,
  string Pane,
  string Color,
  IReadOnlyList<ChartTimePointData> Points,
  IReadOnlyList<ChartRefLineData> RefLines,
  bool Dashed = false,
  bool Hidden = false
);

public sealed record ChartTimePointData(long Time, double Value);

/// <summary>A horizontal reference line of a series (e.g. RSI 70/30).</summary>
public sealed record ChartRefLineData(double Value, string? Label, string Color);

/// <summary>
/// The trigger moment inside the visible window; its label is localized. <c>Direction</c>
/// ("up"/"down"/"neutral") tells the client which way the signal crossed - the arrow must
/// match the alert, never the bar color; null leaves the client to decide from geometry.
/// </summary>
public sealed record ChartTriggerData(long Time, string? Label, string? Direction);

/// <summary>
/// Projects a <see cref="ChartSpec"/> (the render spec) into the interactive
/// <see cref="ChartData"/> payload. Pure, O(points).
/// </summary>
public static class ChartDataMapper
{
  public static ChartData ToChartData(ChartSpec spec) => new(
    spec.Title,
    spec.Candles.Select(ToCandle).ToArray(),
    spec.Series.Select(ToSeries).ToArray(),
    spec.OscMin,
    spec.OscMax,
    spec.TriggerAt is { } triggerAt
      ? new ChartTriggerData(ToUnixSeconds(triggerAt), spec.TriggerLabel, ToDirection(spec.TriggerDirection))
      : null,
    spec.LastCandleAt,
    spec.Extended,
    ToSession(spec.Session));

  private static string? ToSession(MarketSession? session) => session switch
  {
    MarketSession.Regular => "regular",
    MarketSession.PreMarket => "pre",
    MarketSession.AfterHours => "post",
    MarketSession.Overnight => "overnight",
    MarketSession.Closed => "closed",
    _ => null,
  };

  private static string? ToDirection(ChartTriggerDirection? direction) => direction switch
  {
    ChartTriggerDirection.Up => "up",
    ChartTriggerDirection.Down => "down",
    ChartTriggerDirection.Neutral => "neutral",
    _ => null,
  };

  private static ChartCandleData ToCandle(ChartCandle candle) => new(
    ToUnixSeconds(candle.X),
    candle.Open,
    candle.High,
    candle.Low,
    candle.Close,
    candle.Volume);

  private static ChartSeriesData ToSeries(ChartSeries series) => new(
    series.Name,
    series.Kind switch
    {
      ChartSeriesKind.Area => "area",
      ChartSeriesKind.Bar => "bar",
      _ => "line",
    },
    series.Pane == ChartPane.Oscillator ? "oscillator" : "price",
    series.Color,
    series.Points.Select(p => new ChartTimePointData(ToUnixSeconds(p.X), p.Value)).ToArray(),
    series.RefLines.Select(r => new ChartRefLineData(r.Y, r.Label, r.Color)).ToArray(),
    series.Dashed,
    series.HiddenFromLegend);

  /// <summary>
  /// The OADate X (the display wall clock stored as if UTC, see <see cref="ChartX"/>) as Unix
  /// seconds under the same "as if UTC" reading, so the client's UTC-rendered time axis shows
  /// the user's local time.
  /// </summary>
  private static long ToUnixSeconds(double x) =>
    new DateTimeOffset(DateTime.SpecifyKind(ChartX.FromX(x), DateTimeKind.Unspecified), TimeSpan.Zero).ToUnixTimeSeconds();

  private static long ToUnixSeconds(DateTime displayDate) =>
    new DateTimeOffset(DateTime.SpecifyKind(displayDate, DateTimeKind.Unspecified), TimeSpan.Zero).ToUnixTimeSeconds();
}
