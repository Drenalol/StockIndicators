namespace StockIndicators.Domain.Models.Indicators;

/// <summary>
/// Durable anchor store for trailing-stop conditions. The handler reads through to it when
/// its in-memory state is cold (process restart) and writes through whenever the anchor
/// ratchets; conditions removed from a rule are cleaned up by the rule write paths.
/// </summary>
public interface ITrailingStopAnchorStore
{
  /// <summary>Persisted anchor for the condition, or null when nothing was stored yet.</summary>
  Task<double?> GetAnchorAsync(Guid conditionId, CancellationToken cancellationToken = default);

  /// <summary>Upserts the anchor; best-effort - a storage failure never breaks evaluation.</summary>
  Task SetAnchorAsync(Guid conditionId, double anchor, CancellationToken cancellationToken = default);

  /// <summary>Drops anchors of conditions that no longer exist in their rule.</summary>
  Task RemoveAnchorsAsync(IReadOnlyCollection<Guid> conditionIds, CancellationToken cancellationToken = default);
}
