using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Week52HighLow;

public class Week52HighLowIndicatorResult : IndicatorResult
{
  /// <summary>Signed percent drawn in the alert text, per <see cref="Kind"/>.</summary>
  public double Value { get; set; }

  /// <summary>Which of the four alert shapes fired (None while nothing is in its zone).</summary>
  public Week52AlertKind Kind { get; set; }

  /// <summary>Latch: the close was in the high-side zone (new high or pullback) on the previous check.</summary>
  public bool HighActive { get; set; }

  /// <summary>Low-side mirror of <see cref="HighActive"/>.</summary>
  public bool LowActive { get; set; }
}

/// <summary>Identity of the fired alert for the value text: which extreme and which mode.</summary>
public enum Week52AlertKind
{
  None = 0,
  NewHigh,
  NewLow,
  PullbackFromHigh,
  BounceFromLow
}
