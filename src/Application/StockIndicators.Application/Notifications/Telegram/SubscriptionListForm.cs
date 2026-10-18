using Drenalol.Telegram.Bot.Forms;
using MediatR;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Subscriptions.GetSubscriptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

public enum SubscriptionListState
{
  OnPrev,
  OnNext,
  OnSubscription
}

/// <summary>
/// Inline subscription browser: paginated readable rows ("TICKER · INDICATOR") with page
/// navigation; any other text filters the list by ticker (like the instrument browser).
/// Rows used to carry the raw subscription GUID — large accounts overflowed the Telegram
/// reply-markup limits and every menu press failed with "reply markup is too long".
/// </summary>
public class SubscriptionListForm : NavigationFormBase<SubscriptionListState>
{
  private const int PageSize = 20;

  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly TelegramBotOptions _options;

  private string? _search;
  private int _page = 1;

  // Page-scoped label -> subscription id map: labels are readable but not unique
  // by themselves, the ids live here until the next render rebuilds them
  private readonly Dictionary<string, Guid> _idsByLabel = new(StringComparer.OrdinalIgnoreCase);

  public SubscriptionListForm(
    IMediator mediator,
    IChatAccountResolver chatAccountResolver,
    IOptions<TelegramBotOptions> options
  )
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;
    _options = options.Value;

    AddRenderOn(SubscriptionListState.OnPrev, string.Empty, PrevAsync);
    AddRenderOn(SubscriptionListState.OnNext, string.Empty, NextAsync);
    AddRenderOn(SubscriptionListState.OnSubscription, string.Empty, OpenAsync);
  }

  protected override Task<MessageContext> RenderInitAsync(MessageContext context, CancellationToken cancellationToken)
  {
    _search = null;
    _page = 1;

    return RenderListAsync(context, cancellationToken);
  }

  protected override IRender GetRender(MessageContext context)
  {
    try
    {
      return base.GetRender(context);
    }
    catch (InvalidOperationException)
    {
      // Any unregistered text (no command, no button) filters the list
      return CustomRender(SearchAsync);
    }
  }


  private Task<MessageContext> PrevAsync(MessageContext context, CancellationToken cancellationToken)
  {
    if (_page > 1)
      _page--;

    return RenderListAsync(context, cancellationToken);
  }

  private Task<MessageContext> NextAsync(MessageContext context, CancellationToken cancellationToken)
  {
    _page++;

    return RenderListAsync(context, cancellationToken);
  }

  private Task<MessageContext> SearchAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var query = context.Text.Trim();

    if (query.Length == 0)
      return RenderListAsync(context, cancellationToken);

    _search = query;
    _page = 1;

    return RenderListAsync(context, cancellationToken);
  }

  private Task<MessageContext> OpenAsync(MessageContext context, CancellationToken cancellationToken)
  {
    // The row was rendered before and the subscription is gone (deleted meanwhile) —
    // repaint the current page instead of parsing a stale label
    if (!_idsByLabel.TryGetValue(context.Text, out var subscriptionId))
      return RenderListAsync(context, cancellationToken);

    return NavigateToAsync<SubscriptionForm>(context.Forward(subscriptionId.ToString()), cancellationToken);
  }

  private async Task<MessageContext> RenderListAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    if (account is null)
      return context.Reply(BotTexts.ChatNotLinked(language));

    var result = await _mediator.Send(new GetSubscriptionsPageQuery(account.User.Id, _search, _page, PageSize), cancellationToken);

    var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)PageSize));

    if (result.Items.Count == 0 && _page > totalPages)
    {
      _page = totalPages;
      result = await _mediator.Send(new GetSubscriptionsPageQuery(account.User.Id, _search, _page, PageSize), cancellationToken);
    }

    Rows.Clear();
    _buttons.Clear();
    _idsByLabel.Clear();

    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var subscription in result.Items)
    {
      var label = SubscriptionRowCodec.MakeUnique(SubscriptionRowCodec.Encode(subscription), used);

      _idsByLabel[label] = subscription.Id;
      AddRow(label, SubscriptionListState.OnSubscription);
    }

    if (_page > 1)
    {
      var prev = BotTexts.PrevPageButton(language);
      _buttons[prev] = SubscriptionListState.OnPrev;

      if (_page < totalPages)
      {
        var next = BotTexts.NextPageButton(language);
        _buttons[next] = SubscriptionListState.OnNext;

        Rows.Add([new KeyboardButton(prev), new KeyboardButton(next)]);
      }
      else
        Rows.Add([new KeyboardButton(prev)]);
    }
    else if (totalPages > 1)
    {
      // First page: no "back" to draw, so the "forward" button spans the row
      var next = BotTexts.NextPageButton(language);
      _buttons[next] = SubscriptionListState.OnNext;

      Rows.Add([new KeyboardButton(next)]);
    }

    AddBackButton(BotTexts.BackButton(language));

    var text = result.Items.Count == 0 && _search is null
      ? BotTexts.NoSubscriptions(language)
      : BotTexts.SubscriptionListTitle(language, _page, totalPages) + BotTexts.SubscriptionsSearchHint(language, _search);

    // inline markup: the "add" row is a plain url button to the instance's new-subscription page
    return context.Reply(
      text,
      replyMarkup: BotButtons.ToInlineKeyboard(
        Rows,
        [BotButtons.Open(BotTexts.AddSubscriptionButton(language), _options, "dashboard/subscriptions/new")]));
  }
}
