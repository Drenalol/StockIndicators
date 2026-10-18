using System.Collections.Concurrent;
using Serilog;
using StockIndicators.Contracts.Auth;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Configuration.Locale;

/// <summary>
/// Resolves a user's notification locale from their profile. Missing or invalid
/// preferences fall back to <see cref="UserLocale.Default"/> (ru / UTC), and a lookup
/// failure never blocks alert delivery.
///
/// Results are cached in-process for a short TTL: the scheduler resolves the locale
/// for many subscriptions on every tick, and preferences change rarely — a few
/// minutes of staleness after a profile update is acceptable for notifications.
/// </summary>
public class UserLocaleProvider : IUserLocaleProvider
{
  private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
  private const int CacheCapacity = 10_000;

  private readonly IUserRepository _userRepository;
  private readonly ILogger _logger;
  private readonly ConcurrentDictionary<Guid, (UserLocale Locale, DateTime ExpiresAt)> _cache = new();

  public UserLocaleProvider(IUserRepository userRepository, ILogger logger)
  {
    _userRepository = userRepository;
    _logger = logger;
  }

  public async Task<UserLocale> GetAsync(Guid userId, CancellationToken cancellationToken = default)
  {
    if (_cache.TryGetValue(userId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
      return cached.Locale;

    var locale = await ResolveAsync(userId, cancellationToken);

    if (_cache.Count >= CacheCapacity)
      _cache.Clear();

    _cache[userId] = (locale, DateTime.UtcNow.Add(CacheTtl));
    return locale;
  }

  private async Task<UserLocale> ResolveAsync(Guid userId, CancellationToken cancellationToken)
  {
    User? user;

    try
    {
      user = await _userRepository.GetByIdAsync(userId, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.Warning(ex, "Failed to load user {UserId} for locale resolution, using defaults", userId);
      return UserLocale.Default;
    }

    if (user is null)
      return UserLocale.Default;

    var language = UserLocale.IsSupportedLanguage(user.Language) ? user.Language! : UserLocale.DefaultLanguage;
    var timezone = ResolveTimezone(user.Timezone);

    // The chart options are the recipient's persisted document; never set - the client
    // default (tooltip on), same as the web shell resolves
    var showOhlcTooltip = ChartPreferencesDto.Parse(user.ChartPreferences)?.OhlcTooltip ?? true;

    return language == UserLocale.DefaultLanguage && timezone == TimeZoneInfo.Utc && showOhlcTooltip
      ? UserLocale.Default
      : new UserLocale(language, timezone, showOhlcTooltip);
  }

  private static TimeZoneInfo ResolveTimezone(string? timezone)
  {
    if (string.IsNullOrWhiteSpace(timezone))
      return TimeZoneInfo.Utc;

    return TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var found) ? found : TimeZoneInfo.Utc;
  }
}
