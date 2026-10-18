using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace StockIndicators.Application.Providers;

public sealed record ProviderSettingValues(bool IsEnabled, IReadOnlyDictionary<string, string> Credentials);

/// <summary>
/// The provider settings as the application sees them: loaded from the database at startup
/// (before the container is built, when the registration decisions are made), kept live -
/// saves through the settings page and the generated VAPID pair update it in place, and
/// late-bound options resolution reads it through the overlay closures.
/// </summary>
public class ProviderSettingsSnapshot
{
  private readonly ConcurrentDictionary<string, ProviderSettingValues> _rows =
    new(StringComparer.OrdinalIgnoreCase);

  public static ProviderSettingsSnapshot Empty { get; } = new();

  public bool TryGet(string code, out ProviderSettingValues values) => _rows.TryGetValue(code, out values!);

  public IReadOnlyDictionary<string, ProviderSettingValues> Rows => _rows;

  public void Set(string code, ProviderSettingValues values) => _rows[code] = values;

  /// <summary>A stored value for <paramref name="field"/>, or null when absent/blank.</summary>
  public string? Value(string code, string field) =>
    _rows.TryGetValue(code, out var row) && row.Credentials.TryGetValue(field, out var value) && value.Length > 0
      ? value
      : null;

  public bool? IsEnabled(string code) => _rows.TryGetValue(code, out var row) ? row.IsEnabled : null;

  /// <summary>
  /// The rows as they were when startup wired the app. Saves update <see cref="Rows"/> in
  /// place, but what the running app actually uses was decided once, at composition - the
  /// settings page compares against this frozen copy to mark providers whose stored config
  /// is waiting for a restart. Never mutated after <see cref="Freeze"/>.
  /// </summary>
  public IReadOnlyDictionary<string, ProviderSettingValues> StartupRows { get; private set; } =
    new Dictionary<string, ProviderSettingValues>(StringComparer.OrdinalIgnoreCase);

  /// <summary>Whether startup ever froze a copy - an unfrozen snapshot cannot judge "changed since startup".</summary>
  public bool Frozen { get; private set; }

  public void Freeze()
  {
    StartupRows = _rows.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    Frozen = true;
  }
}

/// <summary>
/// The effective configuration view: what the database says wins, the environment
/// (appsettings/env variables, bound the way they always were) is the fallback - an
/// operator who prefers compose environment variables loses nothing.
/// </summary>
public static class EffectiveProviderSettings
{
  public static bool IsEnabled(this ProviderSettingsSnapshot snapshot, IConfiguration configuration, string code)
  {
    var descriptor = ProviderRegistry.Find(code)
                     ?? throw new ArgumentException($"Unknown provider '{code}'", nameof(code));

    return snapshot.IsEnabled(code)
           ?? descriptor.EnvEnabledDefault
           ?? configuration.GetValue<bool>($"{descriptor.EnvSection}:IsEnabled");
  }

  public static bool IsConfigured(this ProviderSettingsSnapshot snapshot, IConfiguration configuration, string code)
  {
    var descriptor = ProviderRegistry.Find(code)
                     ?? throw new ArgumentException($"Unknown provider '{code}'", nameof(code));

    // Auto-configured providers need nothing from the operator; everything else is
    // configured when every required field has a value somewhere
    return descriptor.Fields.All(field => snapshot.HasValue(configuration, descriptor, field));
  }

  public static bool HasValue(
    this ProviderSettingsSnapshot snapshot, IConfiguration configuration, ProviderDescriptor descriptor, ProviderField field) =>
    snapshot.Value(descriptor.Code, field.Name) is not null ||
    configuration[$"{descriptor.EnvSection}:{field.Name}"] is { Length: > 0 };
}
