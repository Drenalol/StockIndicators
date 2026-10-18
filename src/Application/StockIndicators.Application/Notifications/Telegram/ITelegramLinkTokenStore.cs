namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Options for one-time Telegram chat-link tokens (deep links).
/// </summary>
public class TelegramLinkTokenOptions
{
  /// <summary>
  /// Time-to-live of an issued link token, minutes. The token is also single-use.
  /// </summary>
  public int TtlMinutes { get; set; } = 15;

  /// <summary>
  /// Prefix of the deep-link start parameter carrying a chat-link token
  /// (<c>t.me/&lt;bot&gt;?start=link_&lt;token&gt;</c>).
  /// </summary>
  public const string StartParameterPrefix = "link_";

  public static string StartParameter(string token) => StartParameterPrefix + token;
}

/// <summary>
/// An issued one-time chat-link token and its expiry.
/// </summary>
public sealed record IssuedTelegramLinkToken(string Token, DateTime ExpiresAt);

/// <summary>
/// Issues and consumes one-time Telegram chat-link tokens: the web account issues a token,
/// the deep link carries it to the bot (<c>/start link_&lt;token&gt;</c>), and the bot consumes
/// it once to bind the chat to the issuing account. Possession of the short-lived, single-use
/// token is the authorization — no long-lived credentials ever enter the chat.
/// </summary>
public interface ITelegramLinkTokenStore
{
  /// <summary>
  /// Issues a token for the user. A fresh request reuses the still-valid token so repeated
  /// clicks on "Link" do not invalidate a link the user may already be opening.
  /// </summary>
  Task<IssuedTelegramLinkToken> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Consumes a token once: returns the issuing user id on success, null if it is unknown,
  /// expired or already used.
  /// </summary>
  Task<Guid?> ConsumeAsync(string token, CancellationToken cancellationToken = default);
}
