using Drenalol.Telegram.Bot.Forms;
using MediatR;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Account.LinkTelegramChatByDeepLink;
using StockIndicators.Application.Configuration.Locale;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// /start. A linked chat gets the menu; an unlinked one gets directions - the chat is
/// bound only with a one-time token minted by the signed-in web interface (deep link tap
/// here, or "link &lt;token&gt;" typed at the bot). There is no account to create and no
/// consent screen to accept: this is somebody's own instance, and the token is the proof
/// that whoever links the chat already owns the installation.
/// </summary>
public class StartForm : FormBase
{
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly IChatLanguageStore _chatLanguageStore;
  private readonly IMediator _mediator;
  private readonly TelegramBotOptions _options;

  public override bool ClearHistoryOnNavigate => true;
  public override bool IgnoreHistory => true;

  public StartForm(
    IChatAccountResolver chatAccountResolver,
    IChatLanguageStore chatLanguageStore,
    IMediator mediator,
    IOptions<TelegramBotOptions> options
  )
  {
    _chatAccountResolver = chatAccountResolver;
    _chatLanguageStore = chatLanguageStore;
    _mediator = mediator;
    _options = options.Value;
  }

  public override async Task<MessageContext> RenderAsync(MessageContext context, CancellationToken cancellationToken)
  {
    // Deep-link chat linking: "/start link_<one-time-token>" binds this chat to the
    // instance - the token is the credential, nothing is typed into the chat.
    var parameter = StartParameter(context.Text);

    if (parameter is not null && parameter.StartsWith(TelegramLinkTokenOptions.StartParameterPrefix))
      return await LinkByDeepLinkAsync(context, parameter, cancellationToken);

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var name = context.Chat.FirstName ?? "NoName";

    // An unlinked chat has no user row, so its language is the one /language stored per chat
    var language = account is not null
      ? BotTexts.LanguageOf(account.User)
      : UserLocale.NormalizeLanguage(await _chatLanguageStore.GetLanguageAsync(context.Chat.Id, cancellationToken));

    if (account is null)
    {
      if (context.Chat.Type != ChatType.Private)
        return context.Reply(
          BotTexts.GroupChatNotAllowed(language),
          ParseMode.Html,
          replyMarkup: new ReplyKeyboardRemove());

      return context.Reply(
        BotTexts.StartNotLinked(language, name, BotTexts.LinkThisChatButton(language)),
        ParseMode.Html,
        replyMarkup: BotButtons.LinkKeyboard(BotTexts.LinkThisChatButton(language), _options, "dashboard/settings"));
    }

    return context.Reply(
      BotTexts.StartAbout(language, name),
      ParseMode.Html,
      replyMarkup: BuildKeyboard([
        [new KeyboardButton(BotTexts.SubscriptionsButton(language)), new KeyboardButton(BotTexts.IndicatorsButton(language))],
        [new KeyboardButton(BotTexts.InstrumentsButton(language)), new KeyboardButton(BotTexts.SettingsButton(language))]
      ])
    );
  }

  private async Task<MessageContext> LinkByDeepLinkAsync(MessageContext context, string parameter, CancellationToken cancellationToken)
  {
    // The confirmation goes out in the language the chat chose before it had a user row
    var language = UserLocale.NormalizeLanguage(await _chatLanguageStore.GetLanguageAsync(context.Chat.Id, cancellationToken));
    var name = context.Chat.FirstName ?? "NoName";

    var token = parameter[TelegramLinkTokenOptions.StartParameterPrefix.Length..];

    // The bot is personal-only: a group/channel chat can never be linked (alerts would
    // be shared with its members). The token stays unconsumed, so it still works in
    // the user's private chat.
    if (context.Chat.Type != ChatType.Private)
      return context.Reply(BotTexts.GroupChatNotAllowed(language), ParseMode.Html);

    try
    {
      await _mediator.Send(
        new LinkTelegramChatByDeepLinkCommand(context.Chat.Id, token, context.Chat.Username),
        cancellationToken);

      // User.Language answers the language from now on, the per-chat entry is spent
      await _chatLanguageStore.ClearAsync(context.Chat.Id, cancellationToken);

      var welcomeLanguage = BotTexts.LanguageOf(
        (await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken))?.User);

      return context.Reply(
        BotTexts.ChatLinkedConfirmation(language, name),
        ParseMode.Html,
        replyMarkup: BuildKeyboard([
          [new KeyboardButton(BotTexts.SubscriptionsButton(welcomeLanguage)), new KeyboardButton(BotTexts.IndicatorsButton(welcomeLanguage))],
          [new KeyboardButton(BotTexts.InstrumentsButton(welcomeLanguage)), new KeyboardButton(BotTexts.SettingsButton(welcomeLanguage))]
        ])
      );
    }
    catch (TelegramChatOwnedByAnotherAccountException)
    {
      return context.Reply(BotTexts.ChatLinkedToAnother(language), ParseMode.Html);
    }
    catch (TelegramLinkTokenInvalidException)
    {
      return context.Reply(BotTexts.LinkTokenInvalid(language), ParseMode.Html);
    }
  }

  private static string? StartParameter(string text) =>
    text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .ElementAtOrDefault(1);

  private static ReplyKeyboardMarkup BuildKeyboard(KeyboardButton[][] rows) =>
    new(rows)
    {
      ResizeKeyboard = true,
      OneTimeKeyboard = false
    };
}
