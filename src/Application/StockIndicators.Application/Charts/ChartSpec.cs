using StockIndicators.Application.Rules;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Pane an indicator series is drawn on. <c>Price</c> shares the candlestick (left) Y axis,
/// <c>Oscillator</c> is drawn on a secondary (right) axis with its own scale.
/// </summary>
public enum ChartPane
{
  Price = 0,
  Oscillator = 1,
}

/// <summary>How a series is drawn.</summary>
public enum ChartSeriesKind
{
  Line = 0,
  Area = 1,
  Bar = 2,
}

/// <summary>A single (x, y) sample. X is OADate (see <see cref="ChartX"/>).</summary>
public readonly record struct ChartPoint(double X, double Value);

/// <summary>A horizontal reference line (e.g. RSI overbought/oversold, ADX threshold).</summary>
public sealed record ChartRefLine(double Y, string? Label, string Color);

/// <summary>
/// One overlay line/area/bar for an indicator. <c>Dashed</c> marks the thin dashed style of
/// the deviation-band boundaries (the tolerance zone an indicator's crossover condition
/// filters noise with); <c>HiddenFromLegend</c> keeps such helper series out of every legend
/// and legend-chip list while the line itself stays drawn.
/// </summary>
public sealed record ChartSeries(
  string Name,
  ChartSeriesKind Kind,
  ChartPane Pane,
  IReadOnlyList<ChartPoint> Points,
  string Color,
  IReadOnlyList<ChartRefLine> RefLines,
  bool Dashed = false,
  bool HiddenFromLegend = false
);

/// <summary>
/// A candlestick sample (x is OADate, see <see cref="ChartX"/>). Volume is only consumed by
/// the interactive payload, hence the default.
/// </summary>
public readonly record struct ChartCandle(double X, double Open, double High, double Low, double Close, double Volume = 0);

/// <summary>
/// Signal direction of a trigger marker: a buy-side cross, a sell-side cross, or a
/// directionless event (drawn as a neutral dot instead of an arrow).
/// </summary>
public enum ChartTriggerDirection
{
  Up = 0,
  Down = 1,
  Neutral = 2,
}

/// <summary>
/// Everything a trigger chart is drawn from: candles, indicator overlays,
/// the trigger marker and optional oscillator-axis range hints.
/// </summary>
public sealed record ChartSpec(
  string Title,
  IReadOnlyList<ChartCandle> Candles,
  IReadOnlyList<ChartSeries> Series,
  bool HasOscillator,
  double? OscMin,
  double? OscMax,
  DateTime? TriggerAt = null,
  string? TriggerLabel = null,
  ChartTriggerDirection? TriggerDirection = null,
  // UTC date of the newest candle in the window (not the display re-stamp): the live
  // freshness anchor of the chart
  DateTime? LastCandleAt = null,
  // The extended-hours quote line of a daily chart (see ExtendedQuoteData)
  ExtendedQuoteData? Extended = null,
  // The exchange's market phase at build time, projected to a string by ChartDataMapper
  MarketSession? Session = null
);
