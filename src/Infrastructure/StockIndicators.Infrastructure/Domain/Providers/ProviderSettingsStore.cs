using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using StockIndicators.Application.Providers;
using StockIndicators.Domain.Models.Providers;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Providers;

public class ProviderSettingsStore : IProviderSettingsStore
{
  private readonly StockIndicatorsContext _context;
  private readonly ProviderSettingsSnapshot _snapshot;

  public ProviderSettingsStore(StockIndicatorsContext context, ProviderSettingsSnapshot snapshot)
  {
    _context = context;
    _snapshot = snapshot;
  }

  public async Task<IReadOnlyDictionary<string, ProviderSettingValues>> LoadAllAsync(CancellationToken cancellationToken = default)
  {
    var rows = await _context.ProviderSettings.AsNoTracking().ToListAsync(cancellationToken);

    return rows.ToDictionary(
      row => row.Id,
      row => new ProviderSettingValues(row.IsEnabled, Parse(row.Credentials)),
      StringComparer.OrdinalIgnoreCase);
  }

  public async Task SaveAsync(
    string code,
    bool isEnabled,
    IReadOnlyDictionary<string, string> credentials,
    CancellationToken cancellationToken = default
  )
  {
    var row = await _context.ProviderSettings.FindAsync([code], cancellationToken);

    if (row is null)
    {
      row = new ProviderSetting
      {
        Id = code,
        Created = DateTime.UtcNow,
      };

      _context.ProviderSettings.Add(row);
    }

    row.IsEnabled = isEnabled;
    row.Credentials = JsonConvert.SerializeObject(credentials);
    row.Updated = DateTime.UtcNow;

    await _context.SaveChangesAsync(cancellationToken);

    // the live snapshot is what late-bound options closures read - keep it honest
    _snapshot.Set(code, new ProviderSettingValues(isEnabled, credentials));
  }

  private static IReadOnlyDictionary<string, string> Parse(string json) =>
    JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
}
