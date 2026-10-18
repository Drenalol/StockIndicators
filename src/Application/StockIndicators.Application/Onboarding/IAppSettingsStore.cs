namespace StockIndicators.Application.Onboarding;

/// <summary>
/// Tiny key-value store for UI-owned application state (see the AppSettings table).
/// Values are JSON objects; the owning feature decides the shape.
/// </summary>
public interface IAppSettingsStore
{
  /// <summary>The stored JSON for a key, or null when nothing was ever saved.</summary>
  Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

  Task SetAsync(string key, string valueJson, CancellationToken cancellationToken = default);
}
