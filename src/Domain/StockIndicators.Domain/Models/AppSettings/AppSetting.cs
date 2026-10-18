namespace StockIndicators.Domain.Models.AppSettings;

/// <summary>
/// One piece of application state the UI owns and persists: keyed by a stable name, held
/// as a JSON object. Provider credentials have their own typed table; this one is for the
/// small "what the operator saw/did" facts (onboarding dismissal and friends) that are too
/// thin for a table each and must survive reinstalls of the container, not of the volume.
/// </summary>
public class AppSetting : UpdatableEntity<string>
{
  /// <summary>JSON object of the state (shape owned by the feature that owns the key).</summary>
  public string Value { get; set; } = "{}";
}
