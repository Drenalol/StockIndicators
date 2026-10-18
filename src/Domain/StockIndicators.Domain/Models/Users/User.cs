namespace StockIndicators.Domain.Models.Users;

/// <summary>
/// The one account of this installation. Owns subscriptions, push subscriptions,
/// notification templates and the linked Telegram chat. The sign-in password is not here
/// - it comes from configuration (see <c>LocalAuthOptions</c>).
/// </summary>
public class User : UpdatableEntity<Guid>
{
  /// <summary>
  /// Contact address, reserved for the email alert channel; the single local account is
  /// provisioned with a placeholder. Nothing signs anybody in with it.
  /// </summary>
  public string? Email { get; set; }

  public string? DisplayName { get; set; }

  public string? Language { get; set; }

  public string? Timezone { get; set; }

  /// <summary>
  /// Preferred sort of the subscriptions list ("alertDate", "alertCount", "ticker", "created");
  /// null — the default ("alertDate"). See <c>StockIndicators.Domain.Models.Subscriptions.SubscriptionsSort</c>.
  /// </summary>
  public string? SubscriptionsSort { get; set; }

  /// <summary>
  /// Interactive-chart display preferences as a jsonb document (<c>{"OhlcTooltip":true,"Magnet":false}</c>);
  /// null — never set, the client resolves its own defaults. See
  /// <c>StockIndicators.Contracts.Auth.ChartPreferencesDto</c> (the document's shape and merge rules).
  /// </summary>
  public string? ChartPreferences { get; set; }

  /// <summary>
  /// When the user finished or skipped the first-login onboarding tour; null — not shown yet.
  /// </summary>
  public DateTime? InitOnboardingCompletedAt { get; set; }

  /// <summary>
  /// When the user finished or skipped the create-subscription page onboarding tour; null — not shown yet.
  /// </summary>
  public DateTime? SubscriptionOnboardingCompletedAt { get; set; }
}
