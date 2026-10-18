using Drenalol.Telegram.Bot.Forms;
using Drenalol.Telegram.Bot.Forms.TimeZones;
using System.Net;
using Microsoft.Extensions.Options;
using StockIndicators.Domain.Models.Telegram;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

public enum SettingsState
{
  OnLanguage,
  OnTimezone
}

/// <summary>
/// Settings hub: current language and time zone (both editable inline via sub-forms) plus the
/// linked chat. No API keys here - provider credentials live in the web interface.
/// </summary>
public class SettingsForm : NavigationFormBase<SettingsState>
{
  // Clearing the back-stack on entry keeps the chat state consistent with the
  // (possibly just-changed) UI language: a stale-language screen from before
  // the switch can no longer resurface on "Назад". Back from settings → /start.
  public override bool ClearHistoryOnNavigate => true;

  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly TelegramBotOptions _options;

  public SettingsForm(
    IChatAccountResolver chatAccountResolver,
    IOptions<TelegramBotOptions> options
  )
  {
    _chatAccountResolver = chatAccountResolver;
    _options = options.Value;

    AddFormRenderOn<LanguageListForm>(SettingsState.OnLanguage);
    AddFormRenderOn<TimeZoneListForm>(SettingsState.OnTimezone);
  }

  protected override async Task<MessageContext> RenderInitAsync(MessageContext context, CancellationToken cancellationToken)
  {
    Rows.Clear();
    _buttons.Clear();

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    if (account is null)
      return context.Reply(
        BotTexts.SettingsUnlinked(language),
        ParseMode.Html,
        replyMarkup: BotButtons.LinkKeyboard(BotTexts.LinkThisChatButton(language), _options, "dashboard/settings"));

    var timezone = TelegramTimeZones.FormatLabel(account.User.Timezone ?? "UTC");

    var chatLines = account.Chats.Count == 0
      ? BotTexts.NoChats(language)
      : string.Join(
        "\n",
        account.Chats.Select(chat => BotTexts.ChatLine(language, ChatLabel(chat))));

    var text =
      $"<b>{BotTexts.SettingsTitle(language)}</b>\n\n" +
      $"{BotTexts.SettingsLanguageLabel(language)}: {BotTexts.LanguageDisplayName(language)}\n" +
      $"{BotTexts.SettingsTimezoneLabel(language)}: {timezone}\n\n" +
      $"{BotTexts.LinkedChatsHeader(language)}\n{chatLines}";

    AddRow(BotTexts.SettingsLanguageLabel(language), SettingsState.OnLanguage);
    AddRow(BotTexts.SettingsTimezoneLabel(language), SettingsState.OnTimezone);

    AddBackButton(BotTexts.BackButton(language));

    // inline markup: the chats row is a plain url button to the instance's settings page
    return context.Reply(
      text,
      ParseMode.Html,
      replyMarkup: BotButtons.ToInlineKeyboard(
        Rows,
        [BotButtons.Open(BotTexts.ManageChatsButton(language), _options, "dashboard/settings")]));
  }

  private static string ChatLabel(TelegramChat chat) =>
    string.IsNullOrWhiteSpace(chat.Username) ? chat.ChatId.ToString() : $"@{chat.Username} ({chat.ChatId})";
}
