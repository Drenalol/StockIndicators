using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.TrailingStop;

public class TrailingStopIndicatorResult : IndicatorResult
{
  /// <summary>Anchor extreme (long: highest high since arming; short: lowest low). Null until armed.</summary>
  public double? Anchor { get; set; }

  /// <summary>The trailing stop line derived from <see cref="Anchor"/> and the template distance.</summary>
  public double Line { get; set; }

  /// <summary>Signed percent of the close against the anchor: negative under a long's max, positive above a short's min.</summary>
  public double Change { get; set; }

  /// <summary>Close position against the stop line, the cross latch (None before the first check).</summary>
  public TrailingSide Side { get; set; }

  /// <summary>True for a long stop - picks the "from max" alert text over "from min".</summary>
  public bool LongPosition { get; set; }
}

/// <summary>Close position against the trailing stop line.</summary>
public enum TrailingSide
{
  None = 0,
  Above,
  Below
}
