using StockIndicators.Application.Configuration.Locale;

namespace StockIndicators.Application.Notifications.Telegram;

public class TelegramBotOptions
{
  /// <summary>Bot token from @BotFather. Empty = the bot is off (see <see cref="IsConfigured"/>).</summary>
  public string Token { get; set; } = string.Empty;

  /// <summary>
  /// A self-hosted installation is perfectly usable without a bot: the alert pipeline simply
  /// has no Telegram sender, and every other channel keeps delivering.
  /// </summary>
  public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);

  /// <summary>
  /// The address the instance is reached at from outside ("https://indicators.example",
  /// a tunnel's address) - the bot's link buttons are built from it, and Telegram checks
  /// those URLs on its own servers (see <see cref="InstanceUrlIsPublic"/>). Only in use
  /// while <see cref="IncludeWebLinks"/> is on.
  /// </summary>
  public string InstanceUrl { get; set; } = "http://localhost:8080";

  public string InstanceUrlFor(string path) =>
    $"{InstanceUrl.TrimEnd('/')}/{path.TrimStart('/')}";

  /// <summary>
  /// Whether <see cref="InstanceUrl"/> is an address link buttons may point at. Telegram
  /// validates every button URL itself, so a loopback or LAN address does not just look
  /// ugly - the message carrying it is refused outright.
  /// </summary>
  public bool InstanceUrlIsPublic => PublicWebAddress.IsReachable(InstanceUrl);

  /// <summary>
  /// Whether bot messages carry links to the web interface at all. Off by default: a
  /// self-hosted instance is perfectly usable with a bot that only speaks text, and the
  /// links are the one part that needs an address reachable from outside.
  /// </summary>
  public bool IncludeWebLinks { get; set; }

  /// <summary>
  /// Whether a url button may be built: the operator asked for links and the configured
  /// address is one Telegram accepts. When it is not, the buttons stay away - a message
  /// with a rejected URL is refused as a whole, so the bot would deliver nothing at all.
  /// </summary>
  public bool CanSendWebLinks => IncludeWebLinks && InstanceUrlIsPublic;

  /// <summary>
  /// Link to this bot shown in the web settings ("Open the bot in Telegram"),
  /// e.g. https://t.me/my_indicators_bot.
  /// </summary>
  public string BotLink { get; set; } = string.Empty;

  /// <summary>
  /// Deep link that opens a private chat with the bot and passes a start parameter
  /// (<c>t.me/&lt;bot&gt;?start=&lt;parameter&gt;</c>). The parameter is derived from
  /// <see cref="BotLink"/> so no separate bot-username config is needed. Null when the
  /// bot is not configured - a relative "?start=" link would open nonsense.
  /// </summary>
  public string? DeepLink(string startParameter) => BotBaseUri.Length == 0 ? null : $"{BotBaseUri}?start={Uri.EscapeDataString(startParameter)}";

  private string BotBaseUri
  {
    get
    {
      if (!Uri.TryCreate(BotLink, UriKind.Absolute, out var uri))
        return string.Empty;

      return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath.TrimEnd('/')}";
    }
  }
}
