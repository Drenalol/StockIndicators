using Drenalol.Telegram.Bot.Forms;
using System.Net;
using MediatR;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.GetInstrument;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Instrument card rendered inline (the incoming message text is a row label
/// encoded by <see cref="InstrumentRowCodec"/>). Subscribing is delegated to
/// the web interface (strictly), so the only action button opens it.
/// </summary>
public class InstrumentDetailForm : FormBase
{
  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly TelegramBotOptions _options;

  // The instrument this instance renders. The framework re-renders a form restored from
  // the chat's history with whatever text triggered the re-entry - a "Назад" press, not
  // a row label - and the codec decodes any text into some ticker, so the key decoded
  // from the first render (the only one that carries a label) is replayed afterwards.
  private (string Ticker, string? Exchange)? _key;

  public InstrumentDetailForm(IMediator mediator, IChatAccountResolver chatAccountResolver, IOptions<TelegramBotOptions> options)
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;
    _options = options.Value;
  }

  public override async Task<MessageContext> RenderAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    _key ??= InstrumentRowCodec.Decode(context.Text);
    var (ticker, exchange) = _key.Value;

    InstrumentDetailDto detail;

    try
    {
      detail = await _mediator.Send(new GetInstrumentQuery(ticker, exchange), cancellationToken);
    }
    catch (InstrumentNotFoundException exception)
    {
      return context.Reply(exception.Message);
    }

    var subscribePath =
      $"dashboard/subscriptions/new?ticker={Uri.EscapeDataString(detail.Ticker)}" +
      (string.IsNullOrWhiteSpace(detail.ExchangeName) ? string.Empty : $"&exchange={Uri.EscapeDataString(detail.ExchangeName)}");

    // Inline markup: the web_app button only gets initData as an inline button
    var keyboard = BotButtons.ToInlineKeyboard(
      [CreateBackButtonRow(BotTexts.BackButton(language))],
      [BotButtons.Open(BotTexts.SubscribeButton(language), _options, subscribePath)]);

    return context.Reply(BuildText(detail, language), ParseMode.Html, replyMarkup: keyboard);
  }

  private static string BuildText(InstrumentDetailDto detail, string language)
  {
    var title = string.IsNullOrWhiteSpace(detail.ExchangeName)
      ? detail.Ticker
      : $"{detail.Ticker} ({detail.ExchangeName})";

    var lines = new List<string> { $"<b>{WebUtility.HtmlEncode(title)}</b>" };

    var name = detail.LongName ?? detail.Name;

    if (!string.IsNullOrWhiteSpace(name))
      Add(lines, BotTexts.InstrumentFieldName(language), name);

    Add(lines, BotTexts.InstrumentFieldType(language), detail.Type);
    Add(lines, BotTexts.InstrumentFieldCurrency(language), detail.Currency);
    Add(lines, "ISIN", detail.Isin);
    Add(lines, BotTexts.InstrumentFieldSector(language), detail.Sector);
    Add(lines, BotTexts.InstrumentFieldIndustry(language), detail.Industry);
    Add(lines, BotTexts.InstrumentFieldCountry(language), detail.Country);

    return string.Join("\n", lines);

    static void Add(List<string> lines, string label, string? value)
    {
      if (!string.IsNullOrWhiteSpace(value))
        lines.Add($"{label}: {WebUtility.HtmlEncode(value)}");
    }
  }
}
