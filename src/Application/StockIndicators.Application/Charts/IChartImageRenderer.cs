using StockIndicators.Application.Configuration.Locale;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Server-side chart image rendering. The alert channels carry a PNG picture of the chart
/// (push and messenger images cannot render live chart code), and the picture must be the
/// picture the user sees in the app - so it is drawn by the app's own chart core: the
/// renderer drives a headless browser that loads the frontend's self-contained
/// renderer page (<c>frontend/dist/render/renderer.html</c>) and screenshots it.
/// </summary>
public interface IChartImageRenderer
{
  /// <summary>
  /// Renders one interactive chart payload to PNG. The locale drives the image's language
  /// (legend captions) and carries the recipient's chart display options applied to the
  /// static picture (the OHLC/volume row is pinned on the trigger candle when the tooltip
  /// option is on). Null when the image cannot be produced at all (renderer not
  /// configured); rendering failures throw and the callers degrade to a text-only alert.
  ///
  /// <paramref name="options"/> picks the picture's shape: null keeps the alert image the
  /// alert channels have always sent, <see cref="ChartRenderOptions.Card"/> asks for the
  /// wide link-preview card with a headline drawn over the chart.
  /// </summary>
  Task<byte[]?> RenderAsync(
    ChartData chartData,
    UserLocale locale,
    ChartRenderOptions? options = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for <see cref="IChartImageRenderer"/>. Bound from the "ChartImage" section.
/// </summary>
public sealed class ChartImageOptions
{
  /// <summary>
  /// Absolute path to the built renderer page (frontend "npm run build:render" output,
  /// dist/render/renderer.html). Null or missing - image rendering is switched off and
  /// alerts go out without a picture.
  /// </summary>
  public string? PageHtmlPath { get; set; }
}
