using Drenalol.Telegram.Bot.Forms;
using System.Net;
using MediatR;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Indicators.GetIndicatorSchema;
using StockIndicators.Domain.Models.Indicators;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Indicator docs rendered inline (the incoming message text is the enum name):
/// localized description, parameters with defaults and hints. Creating a
/// subscription is delegated to the web app.
/// </summary>
public class IndicatorDetailForm : FormBase
{
  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly TelegramBotOptions _options;

  // The indicator this instance renders. The framework re-renders a form restored from
  // the chat's history with whatever text triggered the re-entry - a "Назад" press, not
  // the enum name - so it is captured on the first render and replayed afterwards.
  private Indicator? _indicator;

  public IndicatorDetailForm(IMediator mediator, IChatAccountResolver chatAccountResolver, IOptions<TelegramBotOptions> options)
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;
    _options = options.Value;
  }

  public override async Task<MessageContext> RenderAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    if (_indicator is null && Enum.TryParse<Indicator>(context.Text, true, out var parsed))
      _indicator = parsed;

    if (_indicator is not { } indicator)
      return context.Reply(BotTexts.UnknownIndicator(language));

    IndicatorSchemaDto schema;

    try
    {
      schema = await _mediator.Send(new GetIndicatorSchemaQuery(indicator, language), cancellationToken);
    }
    catch
    {
      return context.Reply(BotTexts.TemporarilyUnavailable(language));
    }

    var text = BuildText(schema, language);

    // Inline markup: the web_app button only gets initData as an inline button
    var keyboard = BotButtons.ToInlineKeyboard(
      [CreateBackButtonRow(BotTexts.BackButton(language))],
      [BotButtons.Open(BotTexts.CreateSubscriptionButton(language), _options, $"dashboard/subscriptions/new?indicator={Uri.EscapeDataString(schema.Name)}")]);

    return context.Reply(text, ParseMode.Html, replyMarkup: keyboard);
  }

  private static string BuildText(IndicatorSchemaDto schema, string language)
  {
    var lines = new List<string> { $"<b>{WebUtility.HtmlEncode(schema.Name)}</b>" };

    if (!string.IsNullOrWhiteSpace(schema.Description))
      lines.Add(WebUtility.HtmlEncode(schema.Description));

    lines.Add(BotTexts.IndicatorParametersHeader(language));

    if (schema.Properties.Count == 0)
      lines.Add(BotTexts.IndicatorNoParameters(language));
    else
      foreach (var (name, _) in schema.Properties)
      {
        var defaultText = schema.Defaults.TryGetValue(name, out var def) && !string.IsNullOrEmpty(def) ? def : "-";
        var hint = schema.Descriptions.TryGetValue(name, out var description) ? $" - {WebUtility.HtmlEncode(description)}" : string.Empty;

        lines.Add($"• {name} = {WebUtility.HtmlEncode(defaultText)}{hint}");
      }

    return string.Join("\n", lines);
  }
}
