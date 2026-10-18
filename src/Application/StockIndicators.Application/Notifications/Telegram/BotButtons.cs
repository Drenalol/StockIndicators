using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Button factory. Links to the web interface are plain <c>url</c> buttons: the instance
/// is a website the operator opened in the same browser, no mini-app shell and no signed
/// initData around it. Link URLs are language-neutral ({InstanceUrl}/{path}) - the
/// frontend localizes from the profile, so one link works for every language.
/// Everything here stays silent when the bot is not allowed to link to the instance
/// (<see cref="TelegramBotOptions.CanSendWebLinks"/>): a message carrying a URL Telegram
/// refuses is rejected as a whole, so a button that may not be sent is no button at all.
/// </summary>
public static class BotButtons
{
  public static InlineKeyboardButton? Open(string text, TelegramBotOptions options, string path) =>
    options.CanSendWebLinks ? InlineKeyboardButton.WithUrl(text, options.InstanceUrlFor(path)) : null;

  /// <summary>A one-button inline keyboard, or null when the bot carries no links.</summary>
  public static InlineKeyboardMarkup? LinkKeyboard(string text, TelegramBotOptions options, string path) =>
    Open(text, options, path) is { } button ? new InlineKeyboardMarkup([[button]]) : null;

  /// <summary>
  /// Converts reply-keyboard rows to an inline keyboard, appending an optional row of
  /// url buttons (navigation rows replay their text as callback data, which the existing
  /// text router already speaks). Unusable url buttons are dropped, and with them the row:
  /// an empty keyboard row is a 400 of its own.
  /// </summary>
  public static InlineKeyboardMarkup ToInlineKeyboard(
    IEnumerable<IEnumerable<KeyboardButton>> rows,
    IEnumerable<InlineKeyboardButton?>? urlRow = null
  )
  {
    var inline = rows.Select(row => row.Select(AsInline).ToList()).ToList();

    var links = urlRow?.OfType<InlineKeyboardButton>().ToList();

    if (links is { Count: > 0 })
      inline.Add(links);

    return new InlineKeyboardMarkup(inline.Select(row => row.ToArray()));
  }

  public static InlineKeyboardButton AsInline(KeyboardButton button)
  {
    var text = button.Text ?? throw new ArgumentException("Keyboard button without a text cannot become an inline button", nameof(button));

    return InlineKeyboardButton.WithCallbackData(text, text);
  }
}
