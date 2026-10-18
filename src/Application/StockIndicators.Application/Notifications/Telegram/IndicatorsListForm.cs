using Drenalol.Telegram.Bot.Forms;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Notifications.Telegram;

public enum IndicatorsListState
{
  OnIndicator
}

/// <summary>
/// Inline indicator browser: one reply-keyboard row per supported indicator,
/// row text is the indicator enum name (the detail form reads it from the text).
/// No "add subscription" here — it only makes sense after picking an indicator,
/// so the button lives in the indicator detail instead.
/// </summary>
public class IndicatorsListForm : NavigationFormBase<IndicatorsListState>
{
  private readonly IChatAccountResolver _chatAccountResolver;

  public IndicatorsListForm(
    IChatAccountResolver chatAccountResolver
  )
  {
    _chatAccountResolver = chatAccountResolver;

    AddFormRenderOn<IndicatorDetailForm>(IndicatorsListState.OnIndicator);
  }

  protected override async Task<MessageContext> RenderInitAsync(MessageContext context, CancellationToken cancellationToken)
  {
    Rows.Clear();
    _buttons.Clear();

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);
    var language = BotTexts.LanguageOf(account?.User);

    foreach (var name in typeof(Indicator).GetEnumNames())
      AddRow(name, IndicatorsListState.OnIndicator);

    AddBackButton(BotTexts.BackButton(language));

    return context.Reply(BotTexts.IndicatorsListTitle(language), replyMarkup: BuildKeyboard());
  }
}
