using Newtonsoft.Json;
using Npgsql;
using Serilog;
using StockIndicators.Application.Providers;

namespace StockIndicators.Infrastructure.Domain.Providers;

/// <summary>
/// Reads the provider settings table before the container exists - the registration
/// decisions (which providers to wire at all, whether the bot gets a token) are made in
/// ConfigureServices, and migrations have not run yet on a fresh install. A missing table,
/// an unreachable database, any failure: an empty snapshot, and the environment fallback
/// carries the startup exactly as it did before the settings page existed.
/// </summary>
public static class ProviderSettingsBootstrap
{
  /// <summary>
  /// A frozen snapshot with no rows: what a public-site instance runs on (there are no
  /// providers to wire), and what a failed read falls back to.
  /// </summary>
  public static ProviderSettingsSnapshot Empty()
  {
    var snapshot = new ProviderSettingsSnapshot();
    snapshot.Freeze();

    return snapshot;
  }

  public static ProviderSettingsSnapshot Load(string? connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
      return Empty();

    var snapshot = new ProviderSettingsSnapshot();

    try
    {
      using var dataSource = NpgsqlDataSource.Create(connectionString);
      using var connection = dataSource.OpenConnection();
      using var command = connection.CreateCommand();

      command.CommandText = """SELECT "Id", "IsEnabled", "Credentials" FROM "ProviderSettings" """;

      using var reader = command.ExecuteReader();

      while (reader.Read())
      {
        var credentials = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.GetString(2)) ?? [];

        snapshot.Set(reader.GetString(0), new ProviderSettingValues(reader.GetBoolean(1), credentials));
      }
    }
    catch (Exception exception)
    {
      // first start (no table yet) or database still warming up - not an error worth alarming over
      Log.Information(
        exception,
        "Provider settings are unavailable at startup ({Reason}); environment configuration is used until the database is reachable",
        exception.Message);
    }

    // what startup wired: the settings page compares live rows against this copy
    snapshot.Freeze();
    return snapshot;
  }
}
