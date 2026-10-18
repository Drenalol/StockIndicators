using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Providers;

public interface IProviderSettingsStore
{
  /// <summary>Stored rows keyed by provider code (what the database says, no env fallback).</summary>
  Task<IReadOnlyDictionary<string, ProviderSettingValues>> LoadAllAsync(CancellationToken cancellationToken = default);

  /// <summary>Inserts or replaces the row of one provider.</summary>
  Task SaveAsync(string code, bool isEnabled, IReadOnlyDictionary<string, string> credentials, CancellationToken cancellationToken = default);
}

public class UnknownProviderException(string code) : NotFoundException($"There is no provider '{code}' to configure.");
