using Microsoft.EntityFrameworkCore;
using StockIndicators.Application.Onboarding;
using StockIndicators.Domain.Models.AppSettings;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.AppSettings;

public class AppSettingsStore : IAppSettingsStore
{
  private readonly StockIndicatorsContext _context;

  public AppSettingsStore(StockIndicatorsContext context) => _context = context;

  public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
    _context.AppSettings.AsNoTracking().Where(setting => setting.Id == key).Select(setting => setting.Value).FirstOrDefaultAsync(cancellationToken);

  public async Task SetAsync(string key, string valueJson, CancellationToken cancellationToken = default)
  {
    var row = await _context.AppSettings.FindAsync([key], cancellationToken);

    if (row is null)
    {
      row = new AppSetting
      {
        Id = key,
        Created = DateTime.UtcNow,
      };

      _context.AppSettings.Add(row);
    }

    row.Value = valueJson;
    row.Updated = DateTime.UtcNow;

    await _context.SaveChangesAsync(cancellationToken);
  }
}
