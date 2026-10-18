namespace StockIndicators.Contracts.Providers;

/// <summary>
/// One configuration field with its current state. <see cref="Value"/> carries the full
/// effective value for every kind - the settings page is auth-guarded, and the UI masks
/// secrets behind a reveal toggle.
/// </summary>
public record ProviderFieldDto(
  string Name,
  string Label,
  string Kind,
  bool Required,
  bool HasStored,
  bool HasEnv,
  string? Value,
  // Choice fields: the accepted values the dropdown offers; null for the rest
  string[]? Options = null
);

/// <summary>A provider row of the settings page: what it is, whether it is on and usable.</summary>
public record ProviderDto(
  string Code,
  string DisplayName,
  string Kind,
  string DocsUrl,
  string[] Markets,
  bool AutoConfigured,
  bool Enabled,
  bool Configured,
  bool HasStoredSettings,
  // stored config differs from what this process wired at startup - a restart will apply it
  bool RestartPending,
  ProviderFieldDto[] Fields
);

/// <summary>
/// Stores one provider row. In <see cref="Credentials"/> a null/blank value means "keep
/// what is stored" (secrets are only ever sent when the operator typed a new one);
/// fields listed in <see cref="ClearFields"/> are deleted.
/// </summary>
public record SaveProviderRequest(
  bool IsEnabled,
  Dictionary<string, string?>? Credentials,
  string[]? ClearFields
);

/// <summary>Optionally overrides credentials for the probe (what is on screen, saved or not).</summary>
public record TestProviderRequest(Dictionary<string, string?>? Credentials);

public record ProviderTestResultDto(
  bool Success,
  string Message,
  int LatencyMs
);
