namespace StockIndicators.Application.Auth;

/// <summary>
/// Access to a self-hosted instance. There is one account and one way in: a password the
/// operator sets once. The hosted version's identity stack (Google/VK/Yandex/Telegram OIDC,
/// passkeys, email codes, captcha, session registry) is gone - none of it means anything
/// when the machine, the database and the only user all belong to the same person.
/// </summary>
public class LocalAuthOptions
{
  /// <summary>Configuration section this options object binds to.</summary>
  public const string SectionName = "LocalAuth";

  /// <summary>Name of the session cookie the API issues after a successful sign-in.</summary>
  public const string CookieName = "si_session";

  /// <summary>
  /// Plain password, compared in constant time. Convenient for an instance that only ever
  /// listens on localhost or a tailnet; prefer <see cref="PasswordHash"/> as soon as the app
  /// is reachable from somewhere you do not control.
  /// </summary>
  public string Password { get; set; } = string.Empty;

  /// <summary>
  /// PBKDF2-SHA256 digest in the <c>pbkdf2-sha256$iterations$salt$hash</c> form (generate one
  /// with <c>dotnet run -- hash-password</c>). Wins over <see cref="Password"/> when both are set.
  /// </summary>
  public string PasswordHash { get; set; } = string.Empty;

  /// <summary>How long a signed-in browser stays in without re-authenticating.</summary>
  public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(14);

  /// <summary>
  /// An instance with no password is unreachable by design: the login endpoint refuses
  /// everyone, and the setup instructions have to be followed before the app can be used.
  /// </summary>
  public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(PasswordHash) || !string.IsNullOrWhiteSpace(Password);
}
