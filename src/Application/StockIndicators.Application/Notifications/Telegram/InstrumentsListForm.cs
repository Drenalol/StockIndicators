using Drenalol.Telegram.Bot.Forms;
using MediatR;
using StockIndicators.Application.Instruments.GetInstruments;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

public enum InstrumentsListState
{
  OnPrev,
  OnNext,
  OnInstrument
}

/// <summary>
/// Inline instrument browser: paginated rows ("TICKER · EXCHANGE") with page
/// navigation; any other text filters the list (like the web search). No
/// "add subscription" here — subscribing only makes sense after picking an
/// instrument, so the button lives in the instrument card instead.
/// </summary>
public class InstrumentsListForm : NavigationFormBase<InstrumentsListState>
{
  private const int PageSize = 20;

  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;

  private string? _search;
  private int _page = 1;

  public InstrumentsListForm(
    IMediator mediator,
    IChatAccountResolver chatAccountResolver
  )
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;

    AddFormRenderOn<InstrumentDetailForm>(InstrumentsListState.OnInstrument);
    AddRenderOn(InstrumentsListState.OnPrev, string.Empty, PrevAsync);
    AddRenderOn(InstrumentsListState.OnNext, string.Empty, NextAsync);
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

  private async Task<MessageContext> RenderListAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    var result = await _mediator.Send(new GetInstrumentsQuery(_search, null, _page, PageSize), cancellationToken);

    var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)PageSize));

    if (result.Items.Count == 0 && _page > totalPages)
    {
      _page = totalPages;
      result = await _mediator.Send(new GetInstrumentsQuery(_search, null, _page, PageSize), cancellationToken);
    }

    Rows.Clear();
    _buttons.Clear();

    foreach (var instrument in result.Items)
      AddRow(InstrumentRowCodec.Encode(instrument), InstrumentsListState.OnInstrument);

    if (_page > 1)
    {
      var prev = BotTexts.PrevPageButton(language);
      _buttons[prev] = InstrumentsListState.OnPrev;

      if (_page < totalPages)
      {
        var next = BotTexts.NextPageButton(language);
        _buttons[next] = InstrumentsListState.OnNext;

        Rows.Add([new KeyboardButton(prev), new KeyboardButton(next)]);
      }
      else
        Rows.Add([new KeyboardButton(prev)]);
    }
    else if (totalPages > 1)
    {
      // First page: no "back" to draw, so the "forward" button spans the row
      var next = BotTexts.NextPageButton(language);
      _buttons[next] = InstrumentsListState.OnNext;

      Rows.Add([new KeyboardButton(next)]);
    }

    AddBackButton(BotTexts.BackButton(language));

    var text = result.Items.Count == 0
      ? BotTexts.NoInstruments(language) + BotTexts.InstrumentsSearchHint(language)
      : BotTexts.InstrumentsListTitle(language, _page, totalPages, _search) + BotTexts.InstrumentsSearchHint(language);

    return context.Reply(text, replyMarkup: BuildKeyboard());
  }
}
