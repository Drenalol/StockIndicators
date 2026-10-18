namespace StockIndicators.Domain.Models.Indicators;

/// <summary>
/// Persisted extreme anchor of one trailing-stop condition (the WHEN leaf's condition id is
/// the key). The in-memory indicator state dies with a restart; this row is the cold-start
/// hydration source, so the stop line keeps ratcheting from the extreme reached since the
/// alert was set instead of silently re-anchoring at the current price.
/// </summary>
public class TrailingStopAnchor
{
  /// <summary>Id of the rule condition (WHEN leaf) this anchor belongs to.</summary>
  public Guid ConditionId { get; set; }

  /// <summary>Long stop: the highest high since arming; short stop: the lowest low since arming.</summary>
  public double Anchor { get; set; }

  /// <summary>When the anchor was last ratcheted (UTC).</summary>
  public DateTime Updated { get; set; }
}
