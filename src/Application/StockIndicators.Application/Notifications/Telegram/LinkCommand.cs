using Drenalol.Telegram.Bot.Forms;
using MediatR;
using StockIndicators.Application.Account.LinkTelegramChatByDeepLink;
using StockIndicators.Application.Configuration.Locale;
using Telegram.Bot.Types.Enums;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// "link &lt;one-time token&gt;" binds this chat to the instance. The token is minted by
/// the signed-in web interface - that is the proof of ownership; the deep link from the
/// settings page does the same thing in one tap, this command covers the typed case
/// (and a token pasted from anywhere, "link_abc" included).
/// </summary>
public class LinkCommand : IBotCommand
{
  public string Prefix => "link";

  private readonly IMediator _mediator;
  private readonly IChatLanguageStore _chatLanguageStore;

  public LinkCommand(IMediator mediator, IChatLanguageStore chatLanguageStore)
  {
    _mediator = mediator;
    _chatLanguageStore = chatLanguageStore;
  }

  public async Task<MessageContext> ExecuteAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var language = UserLocale.NormalizeLanguage(
      await _chatLanguageStore.GetLanguageAsync(context.Chat.Id, cancellationToken));

    var parts = context.Text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length < 2)
      return context.Reply(BotTexts.LinkCommandUsage(language), ParseMode.Html);

    var token = parts[1];

    // a start parameter copied out of the deep link still works
    if (token.StartsWith(TelegramLinkTokenOptions.StartParameterPrefix, StringComparison.Ordinal))
      token = token[TelegramLinkTokenOptions.StartParameterPrefix.Length..];

    // The bot is personal-only: alerts would be shared with every group member.
    if (context.Chat.Type != ChatType.Private)
      return context.Reply(BotTexts.GroupChatNotAllowed(language), ParseMode.Html);

    try
    {
      await _mediator.Send(
        new LinkTelegramChatByDeepLinkCommand(context.Chat.Id, token, context.Chat.Username),
        cancellationToken);

      // User.Language answers the language from now on, the per-chat entry is spent
      await _chatLanguageStore.ClearAsync(context.Chat.Id, cancellationToken);

      return context.Reply(
        BotTexts.ChatLinkedConfirmation(language, context.Chat.FirstName ?? "NoName"),
        ParseMode.Html);
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
}
