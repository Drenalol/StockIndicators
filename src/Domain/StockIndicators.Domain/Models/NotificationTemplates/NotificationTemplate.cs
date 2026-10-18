namespace StockIndicators.Domain.Models.NotificationTemplates;

/// <summary>
/// A user-facing alert text template. Channel-agnostic on purpose: the renderer
/// substitutes the same tokens for every channel, the output's own channel picks
/// the sender and the default fallback format.
/// </summary>
public class NotificationTemplate : UpdatableEntity<Guid>
{
  public string Name { get; set; } = null!;
  public bool IsBundled { get; set; }
  public string Template { get; set; } = null!;
  /// <summary>
  /// Owner of a non-bundled template. Null for bundled templates and for
  /// legacy templates created before per-user ownership existed (visible to everyone).
  /// </summary>
  public Guid? UserId { get; set; }
}