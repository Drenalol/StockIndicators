using MediatR;
using Microsoft.Extensions.Configuration;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Providers;

namespace StockIndicators.Application.Providers.GetProviders;

public record GetProvidersQuery : IQuery<IReadOnlyList<ProviderDto>>;

/// <summary>
/// The settings page: every provider the code knows, with the effective state - stored
/// values win over environment ones. Secrets come back in full: the page is auth-guarded
/// and single-user, and the UI masks them behind a per-provider reveal toggle.
/// </summary>
public class GetProvidersQueryHandler : IRequestHandler<GetProvidersQuery, IReadOnlyList<ProviderDto>>
{
  private readonly IProviderSettingsStore _store;
  private readonly IConfiguration _configuration;
  private readonly ProviderSettingsSnapshot _snapshot;

  // the snapshot is optional for tests: without a frozen startup copy nothing can look
  // "waiting for a restart", which is the honest answer when startup state is unknown
  public GetProvidersQueryHandler(
    IProviderSettingsStore store,
    IConfiguration configuration,
    ProviderSettingsSnapshot? startup = null
  )
  {
    _store = store;
    _configuration = configuration;
    _snapshot = startup ?? ProviderSettingsSnapshot.Empty;
  }

  public async Task<IReadOnlyList<ProviderDto>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
  {
    var rows = await _store.LoadAllAsync(cancellationToken);

    return ProviderRegistry.All
      .Select(descriptor => Describe(descriptor, rows, _configuration, _snapshot))
      .ToList();
  }

  private static ProviderDto Describe(
    ProviderDescriptor descriptor,
    IReadOnlyDictionary<string, ProviderSettingValues> rows,
    IConfiguration configuration,
    ProviderSettingsSnapshot snapshot
  )
  {
    rows.TryGetValue(descriptor.Code, out var row);
    snapshot.StartupRows.TryGetValue(descriptor.Code, out var applied);

    var envEnabled = descriptor.EnvEnabledDefault ?? configuration.GetValue<bool>($"{descriptor.EnvSection}:IsEnabled");
    var enabled = row?.IsEnabled ?? envEnabled;

    var fields = descriptor.Fields
      .Select(field =>
      {
        var stored = row is not null && row.Credentials.TryGetValue(field.Name, out var value) && value.Length > 0 ? value : null;
        var env = configuration[$"{descriptor.EnvSection}:{field.Name}"] is { Length: > 0 } fromEnv ? fromEnv : null;

        return new ProviderFieldDto(
          field.Name,
          field.Label,
          field.Kind.ToString().ToLowerInvariant(),
          field.Required,
          HasStored: stored is not null,
          HasEnv: env is not null,
          // every value round-trips in full so the UI can edit and reveal it; bool values
          // read from configuration arrive as "True"/"False" (the config providers
          // stringify .NET booleans) - the UI speaks lowercase 'true'/'false'.
          Value: field.Kind == ProviderFieldKind.Bool
            ? (stored ?? env)?.ToLowerInvariant()
            : stored ?? env,
          field.Options);
      })
      .ToList();

    var configured = descriptor.AutoConfigured || descriptor.Fields
      .Where(field => field.Required)
      .All(field => fields.Single(dto => dto.Name == field.Name) is { HasStored: true } or { HasEnv: true });

    return new ProviderDto(
      descriptor.Code,
      descriptor.DisplayName,
      descriptor.Kind.ToString().ToLowerInvariant(),
      descriptor.DocsUrl,
      descriptor.Markets,
      descriptor.AutoConfigured,
      enabled,
      configured,
      row is not null,
      RestartPending(snapshot, descriptor, configuration, row, applied, envEnabled),
      [.. fields]);
  }

  /// <summary>
  /// Whether the effective state says something other than what this process wired at startup.
  /// Stored values win over environment ones and the environment never moves while the process
  /// runs, so a stored value that repeats what the environment already gives changes nothing -
  /// the raw rows are never compared directly, and a row created by saving env values (or a
  /// cleared field falling back to the same env value) is not a change. Blank credentials never
  /// made it into the wiring, so they do not count as a change either.
  /// </summary>
  private static bool RestartPending(
    ProviderSettingsSnapshot snapshot,
    ProviderDescriptor descriptor,
    IConfiguration configuration,
    ProviderSettingValues? row,
    ProviderSettingValues? applied,
    bool envEnabled
  )
  {
    // without a frozen startup copy nothing can honestly claim to wait for a restart
    if (!snapshot.Frozen)
      return false;

    if ((row?.IsEnabled ?? envEnabled) != (applied?.IsEnabled ?? envEnabled))
      return true;

    return descriptor.Fields.Any(field =>
    {
      // what the wiring reads from a state: the stored value if any, else the environment one
      string? Effective(ProviderSettingValues? from) =>
        from is not null && from.Credentials.TryGetValue(field.Name, out var stored) && stored.Length > 0
          ? stored
          : configuration[$"{descriptor.EnvSection}:{field.Name}"] is { Length: > 0 } fromEnv ? fromEnv : null;

      // the environment spells booleans "True"/"False" while the store keeps "true"/"false"
      return field.Kind == ProviderFieldKind.Bool
        ? !string.Equals(Effective(row), Effective(applied), StringComparison.OrdinalIgnoreCase)
        : Effective(row) != Effective(applied);
    });
  }
}
