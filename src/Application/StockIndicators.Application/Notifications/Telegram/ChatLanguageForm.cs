using Drenalol.Telegram.Bot.Forms;
using MediatR;
using StockIndicators.Application.Account.UpdateUserPreferences;
using StockIndicators.Application.Configuration.Locale;

namespace StockIndicators.Application.Notifications.Telegram;

public enum ChatLanguageState
{
  OnSelect
}

/// <summary>
/// "/language": a language picker that also works while the chat is not linked yet.
/// A linked chat persists through the same UpdateUserPreferences command the web uses
/// (and stays on the list, the settings language picker does the same); an unlinked chat
/// has no user row to write to, so the choice goes to the per-chat store and the bot
/// answers with the start message of the chosen language, which also drops this picker.
/// Any other text re-renders the list for a linked chat and restarts the chat for an
/// unlinked one.
/// </summary>
public class ChatLanguageForm : NavigationFormBase<ChatLanguageState>
{
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly IChatLanguageStore _chatLanguageStore;
  private readonly IMediator _mediator;

  public ChatLanguageForm(
    IChatAccountResolver chatAccountResolver,
    IChatLanguageStore chatLanguageStore,
    IMediator mediator
  )
  {
    _chatAccountResolver = chatAccountResolver;
    _chatLanguageStore = chatLanguageStore;
    _mediator = mediator;

    AddRenderOn(ChatLanguageState.OnSelect, string.Empty, (context, cancellationToken) => SelectLanguageAsync(context.Text, context, cancellationToken));
  }

  protected override async Task<MessageContext> RenderInitAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var language = await CurrentLanguageAsync(context.Chat.Id, cancellationToken);

    return await RenderPickerAsync(language, context);
  }

  protected override IRender GetRender(MessageContext context) =>
    _buttons.TryGetValue(context.Text, out var state)
      ? Renders[state]
      : CustomRender((context, cancellationToken) => RestartAsync(context, cancellationToken));

  private async Task<MessageContext> SelectLanguageAsync(string option, MessageContext context, CancellationToken cancellationToken)
  {
    var code = UserLocale.SupportedLanguages.FirstOrDefault(descriptor => descriptor.NativeName == option)?.Code;

    if (code is null)
      return await RestartAsync(context, cancellationToken);

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);

    if (account is null)
    {
      await _chatLanguageStore.SetLanguageAsync(context.Chat.Id, code, cancellationToken);

      return await NavigateToAsync<StartForm>(new MessageContext(context.Chat, "/start"), cancellationToken);
    }

    try
    {
      await _mediator.Send(new UpdateUserPreferencesCommand(account.User.Id, code, null), cancellationToken);
    }
    catch (Exception exception)
    {
      return context.Reply(exception.Message);
    }

    return await RenderPickerAsync(code, context);
  }

  private async Task<MessageContext> RestartAsync(MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);

    if (account is not null)
      return await RenderPickerAsync(BotTexts.LanguageOf(account.User), context);

    return await NavigateToAsync<StartForm>(new MessageContext(context.Chat, "/start"), cancellationToken);
  }

  private async Task<string> CurrentLanguageAsync(long chatId, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(chatId, cancellationToken);

    return account is not null
      ? BotTexts.LanguageOf(account.User)
      : UserLocale.NormalizeLanguage(await _chatLanguageStore.GetLanguageAsync(chatId, cancellationToken));
  }

  private Task<MessageContext> RenderPickerAsync(string language, MessageContext context)
  {
    Rows.Clear();
    _buttons.Clear();

    foreach (var descriptor in UserLocale.SupportedLanguages)
      AddRow(descriptor.Code == language ? $"✓ {descriptor.NativeName}" : descriptor.NativeName, ChatLanguageState.OnSelect);

    AddBackButton(BotTexts.BackButton(language));

    return Task.FromResult(context.Reply(BotTexts.LanguageListTitle(language), replyMarkup: BuildKeyboard()));
  }
}
