namespace StockIndicators.Application.Charts;

/// <summary>
/// The shape of picture <see cref="IChartImageRenderer"/> must produce. Two shapes exist:
/// the alert image (the historical size the channels have always carried, drawn with the
/// recipient's chart options) and the link-preview card (the size messengers render under
/// a shared URL, with a title and a subtitle drawn over the chart). The alert shape is the
/// default, so an alert path that passes no options renders exactly what it rendered before.
/// </summary>
public sealed record ChartRenderOptions
{
  /// <summary>The renderer page protocol's mode names (frontend render/main.ts).</summary>
  public const string AlertMode = "alert";

  public const string CardMode = "card";

  /// <summary>The alert picture: CSS pixels of the chart area, delivered at 2x (1760x880 PNG).</summary>
  public const int AlertWidth = 880;
  public const int AlertHeight = 440;

  /// <summary>
  /// The link-preview card: the size Open Graph asks for and Telegram, WhatsApp, Discord
  /// and VK all downscale from. Delivered at 1x - a preview is shown ~400 px wide, so the
  /// extra pixels would only cost bandwidth on every unfurl.
  /// </summary>
  public const int CardWidth = 1200;
  public const int CardHeight = 630;

  /// <summary>
  /// Longest subtitle the card draws: the indicator hints are documentation prose, and a
  /// card under a shared link is read in a second, so it gets the opening of the text
  /// rather than the whole of it (truncated on a word boundary, ellipsis appended).
  /// </summary>
  public const int MaxSubtitleLength = 180;

  public string Mode { get; init; } = AlertMode;

  public int Width { get; init; } = AlertWidth;

  public int Height { get; init; } = AlertHeight;

  /// <summary>Device pixels per CSS pixel: the picture is Width x Height x this.</summary>
  public float DeviceScaleFactor { get; init; } = 2f;

  /// <summary>Card only: the headline over the chart (an indicator name).</summary>
  public string? Title { get; init; }

  /// <summary>Card only: the line under the headline (the localized indicator description).</summary>
  public string? Subtitle { get; init; }

  /// <summary>
  /// Card only: the small brand line over the headline. Null draws no line at all - a card
  /// whose headline already is the brand name does not need it twice.
  /// </summary>
  public string? Eyebrow { get; init; } = BrandEyebrow;

  /// <summary>The renderer page protocol's brand line (frontend render/main.ts).</summary>
  public const string BrandEyebrow = "STOCK INDICATORS";

  public static ChartRenderOptions Alert() => new();

  public static ChartRenderOptions Card(string title, string? subtitle = null, string? eyebrow = BrandEyebrow) =>
    new()
    {
      Mode = CardMode,
      Width = CardWidth,
      Height = CardHeight,
      DeviceScaleFactor = 1f,
      Title = title,
      Subtitle = Truncate(subtitle),
      Eyebrow = eyebrow,
    };

  /// <summary>
  /// Cuts the subtitle to <see cref="MaxSubtitleLength"/> on a word boundary and marks the
  /// cut. Null/blank in, null out (the card then draws the headline alone).
  /// </summary>
  public static string? Truncate(string? subtitle)
  {
    var text = subtitle?.Trim();

    if (string.IsNullOrEmpty(text))
      return null;

    if (text.Length <= MaxSubtitleLength)
      return text;

    var cut = text.LastIndexOf(' ', MaxSubtitleLength);

    // a single word longer than the whole budget (a long compound in one of the languages)
    // has no boundary to cut on: hard-cut it instead of dropping the line
    if (cut <= 0)
      return text[..MaxSubtitleLength].TrimEnd() + "…";

    return text[..cut].TrimEnd(' ', ',', ':', ';', '.') + "…";
  }
}
