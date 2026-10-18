using Drenalol.Telegram.Bot.Forms.Localization;
using StockIndicators.Application.Configuration.Locale;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// The bot's own catalog doubles as the framework's text localizer: every supported
/// language is declared here, so a "Назад"/"Back"/"Retour" press routes to the back
/// handler in whichever language the chat speaks, and the framework's pagination arrows
/// are the same buttons the list forms draw themselves.
/// </summary>
public class BotTextsLocalizer : ITelegramTextLocalizer
{
  private static readonly DefaultTextLocalizer Fallback = new();

  public string DefaultLanguage => UserLocale.DefaultLanguage;

  public IReadOnlyList<string> SupportedLanguages { get; } =
    [.. UserLocale.SupportedLanguages.Select(descriptor => descriptor.Code)];

  public string Get(TextKey key, string language) => key switch
  {
    TextKey.Back => BotTexts.BackButton(language),
    TextKey.PreviousPage => BotTexts.PrevPageButton(language),
    TextKey.NextPage => BotTexts.NextPageButton(language),
    _ => Fallback.Get(key, language),
  };
}
