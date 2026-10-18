namespace StockIndicators.Application.Configuration.Locale;

public interface IUserLocaleProvider
{
  /// <summary>
  /// Resolves the user's language and timezone from their profile, falling back to
  /// <see cref="UserLocale.Default"/> for missing or invalid values.
  /// </summary>
  Task<UserLocale> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
