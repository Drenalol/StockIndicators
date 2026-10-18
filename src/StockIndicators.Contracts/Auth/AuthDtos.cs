using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockIndicators.Contracts.Auth;

/// <summary>
/// The one account of this installation: what the UI needs to render itself. There is no
/// email, no external identities and no deletion schedule - the operator is whoever knows the
/// password, and their preferences are the account.
/// </summary>
public record AuthProfileDto(
  string Id,
  string? DisplayName,
  string? Language,
  string? Timezone,
  bool OnboardingCompleted,
  IReadOnlyList<TelegramChatDto> TelegramChats,
  // Preferred subscriptions-list sort; null — the default ("alertDate")
  string? SubscriptionsSort = null,
  // Interactive-chart display preferences; null — the client defaults (tooltip on, magnet off)
  ChartPreferencesDto? ChartPreferences = null,
  // Whether the create-subscription page tour was completed or skipped
  bool SubscriptionTourCompleted = false
);

/// <summary>
/// Interactive-chart display preferences of an account, persisted as a jsonb document in
/// <c>User.ChartPreferences</c> and patched partially (a null flag on a PATCH leaves the
/// stored flag untouched). A whole-null document means "never set": the client resolves the
/// defaults (ohlcTooltip on, magnet off). Unknown JSON members are rejected on input so a
/// typo does not silently drop a preference.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record ChartPreferencesDto(bool? OhlcTooltip = null, bool? Magnet = null)
{
  /// <summary>Reads the stored jsonb document; null when absent or unreadable (never throws).</summary>
  public static ChartPreferencesDto? Parse(string? stored)
  {
    if (string.IsNullOrWhiteSpace(stored))
      return null;

    try
    {
      return JsonSerializer.Deserialize<ChartPreferencesDto>(stored);
    }
    catch (JsonException)
    {
      return null;
    }
  }

  /// <summary>Applies a partial patch flag-by-flag; null when nothing is set (no document to store).</summary>
  public ChartPreferencesDto? Merge(ChartPreferencesDto? current)
  {
    var merged = new ChartPreferencesDto(OhlcTooltip ?? current?.OhlcTooltip, Magnet ?? current?.Magnet);

    return merged.OhlcTooltip is null && merged.Magnet is null ? null : merged;
  }
}

/// <summary>
/// Result of a "revoke all other sessions" call: how many sessions were closed.
/// </summary>
public record RevokedSessionsDto(int Revoked);

/// <summary>
/// The account's Telegram chat (an account has at most one; linking a new chat
/// unlinks the previous one).
/// </summary>
public record TelegramChatDto(
  string Id,
  long ChatId,
  string? Username,
  DateTime LinkedAt
);

/// <summary>
/// One-time Telegram chat-link deep link. The user opens <see cref="DeepLink"/> and taps
/// Start; the bot consumes the embedded token and binds the private chat to the issuing
/// account. The token is single-use and expires at <see cref="ExpiresAt"/>. Groups are
/// not supported: the bot and its alerts are personal-only.
/// </summary>
public record TelegramLinkDto(
  // absolute bot deep link; null when this instance has no Telegram bot
  string? DeepLink,
  DateTime ExpiresAt
);
