using Drenalol.Telegram.Bot.Forms;
using MediatR;
using StockIndicators.Application.Account.UpdateUserPreferences;
using StockIndicators.Application.Configuration.Locale;

namespace StockIndicators.Application.Notifications.Telegram;

public enum LanguageListState
{
  /// <summary>
  /// The single state of this form: "choose the language this button stands for" —
  /// the language code comes from the pressed button's text, so every supported
  /// language maps here and adding one touches no switch.
  /// </summary>
  OnSelect
}

/// <summary>
/// Inline language picker driven by <see cref="UserLocale.SupportedLanguages"/>: adding a
/// language to the registry is enough for it to appear here and work — no per-language
/// switch or constructor edit. Persisting goes through the same UpdateUserPreferences
/// command the web uses, so the bot and the web stay in sync.
/// </summary>
public class LanguageListForm : NavigationFormBase<LanguageListState>
{
  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;

  public LanguageListForm(
    IMediator mediator,
    IChatAccountResolver chatAccountResolver
  )
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;

    AddRenderOn(LanguageListState.OnSelect, string.Empty, (context, cancellationToken) => SetLanguageAsync(context.Text, context, cancellationToken));
  }

  protected override async Task<MessageContext> RenderInitAsync(MessageContext context, CancellationToken cancellationToken)
  {
    Rows.Clear();
    _buttons.Clear();

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);

    if (account is null)
      return context.Reply(BotTexts.ChatNotLinked(BotTexts.LanguageOf(null)));

    var language = BotTexts.LanguageOf(account.User);

    foreach (var descriptor in UserLocale.SupportedLanguages)
      AddRow(Mark(descriptor.NativeName, language, descriptor.Code), LanguageListState.OnSelect);

    AddBackButton(BotTexts.BackButton(language));

    return context.Reply(BotTexts.LanguageListTitle(language), replyMarkup: BuildKeyboard());
  }

  private async Task<MessageContext> SetLanguageAsync(string option, MessageContext context, CancellationToken cancellationToken)
  {
    var code = UserLocale.SupportedLanguages.FirstOrDefault(descriptor => descriptor.NativeName == option)?.Code;

    if (code is null)
      return await NavigateToAsync<SettingsForm>(context, cancellationToken);

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);

    if (account is null)
      return context.Reply(BotTexts.ChatNotLinked(BotTexts.LanguageOf(null)));

    try
    {
      await _mediator.Send(new UpdateUserPreferencesCommand(account.User.Id, code, null), cancellationToken);
    }
    catch (Exception exception)
    {
      return context.Reply(exception.Message);
    }

    // Completed: "Назад" later skips this picker instead of showing it again.
    IsCompleted = true;

    return await NavigateToAsync<SettingsForm>(context, cancellationToken);
  }

  private static string Mark(string option, string current, string code) =>
    code == current ? $"✓ {option}" : option;
}
