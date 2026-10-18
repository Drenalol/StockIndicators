using Drenalol.Telegram.Bot.Forms;
using MediatR;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Subscriptions.GetSubscription;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

public class SubscriptionForm : FormBase
{
  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly TelegramBotOptions _options;

  // The subscription this instance renders. The framework re-renders a form restored
  // from the chat's history with whatever text triggered the re-entry - a "Назад"
  // press, not the forwarded id - so the id is captured on the first render (the only
  // one that carries it) and replayed afterwards.
  private Guid? _subscriptionId;

  public SubscriptionForm(
    IMediator mediator,
    IChatAccountResolver chatAccountResolver,
    IOptions<TelegramBotOptions> options
  )
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;
    _options = options.Value;
  }

  public override async Task<MessageContext> RenderAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    if (account is null)
      return context.Reply(BotTexts.ChatNotLinked(language));

    if (_subscriptionId is null && Guid.TryParse(context.Text, out var parsed))
      _subscriptionId = parsed;

    if (_subscriptionId is not { } subscriptionId)
      return context.Reply(BotTexts.SubscriptionNotFound(language));

    try
    {
      var subscription = await _mediator.Send(new GetSubscriptionQuery(subscriptionId, account.User.Id), cancellationToken);

      var ticker = SubscriptionsText.PrimaryTicker(subscription);

      // the edit button links to the instance, so it is absent both without a ticker and
      // when the bot carries no links at all
      InlineKeyboardButton? editButton = string.IsNullOrWhiteSpace(ticker)
        ? null
        : BotButtons.Open(
            BotTexts.EditSubscriptionButton(language),
            _options,
            $"dashboard/subscriptions/{Uri.EscapeDataString(ticker)}/{subscription.Id}/edit");

      var keyboard = BotButtons.ToInlineKeyboard(
        [CreateBackButtonRow(BotTexts.BackButton(language))],
        editButton is null ? null : [editButton]);

      return context.Reply(SubscriptionsText.Detail(subscription, language), ParseMode.Html, replyMarkup: keyboard);
    }
    // Anything else stays with the framework's guard: it logs the real failure and
    // answers the chat with a neutral localized error - never a raw .NET message
    catch (SubscriptionNotFoundException)
    {
      return context.Reply(BotTexts.SubscriptionNotFound(language));
    }
  }
}
