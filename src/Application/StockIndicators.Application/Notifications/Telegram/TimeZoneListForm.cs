using Drenalol.Telegram.Bot.Forms;
using Drenalol.Telegram.Bot.Forms.TimeZones;
using MediatR;
using StockIndicators.Application.Account.UpdateUserPreferences;

namespace StockIndicators.Application.Notifications.Telegram;

public enum TimeZoneListState
{
  OnTimezone
}

/// <summary>
/// Inline time zone picker: common zones as buttons plus a free-text fallback so
/// any valid IANA id can be set by typing it. Persisted through the same
/// UpdateUserPreferences command the web uses. Button labels carry a friendly
/// offset ("Europe/Moscow · UTC+03:00") that is stripped before lookup, since
/// a reply-keyboard button's label is also its payload.
/// </summary>
public class TimeZoneListForm : NavigationFormBase<TimeZoneListState>
{
  private const string CheckMark = "✓ ";
  private const string OffsetSeparator = " · ";

  private readonly IMediator _mediator;
  private readonly IChatAccountResolver _chatAccountResolver;

  public TimeZoneListForm(
    IMediator mediator,
    IChatAccountResolver chatAccountResolver
  )
  {
    _mediator = mediator;
    _chatAccountResolver = chatAccountResolver;

    AddRenderOn(TimeZoneListState.OnTimezone, string.Empty, (context, cancellationToken) => SelectAsync(context.Text, context, cancellationToken));
  }

  protected override async Task<MessageContext> RenderInitAsync(MessageContext context, CancellationToken cancellationToken)
  {
    Rows.Clear();
    _buttons.Clear();

    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);

    if (account is null)
      return context.Reply(BotTexts.ChatNotLinked(BotTexts.LanguageOf(null)));

    var language = BotTexts.LanguageOf(account.User);
    var current = account.User.Timezone ?? "UTC";

    var zones = TelegramTimeZones.Popular();

    foreach (var (id, label) in zones)
      AddRow(id == current ? $"{CheckMark}{label}" : label, TimeZoneListState.OnTimezone);

    AddBackButton(BotTexts.BackButton(language));

    return context.Reply(
      $"{BotTexts.TimeZoneListTitle(language)}\n\n{string.Join("\n", zones.Select(z => $"• {z.Label}"))}{BotTexts.TimeZoneListHint(language)}",
      replyMarkup: BuildKeyboard());
  }

  protected override IRender GetRender(MessageContext context)
  {
    try
    {
      return base.GetRender(context);
    }
    catch (InvalidOperationException)
    {
      return CustomRender((context, cancellationToken) => SelectAsync(context.Text, context, cancellationToken));
    }
  }

  private async Task<MessageContext> SelectAsync(string text, MessageContext context, CancellationToken cancellationToken)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(context.Chat.Id, cancellationToken);

    if (account is null)
      return context.Reply(BotTexts.ChatNotLinked(BotTexts.LanguageOf(null)));

    var language = BotTexts.LanguageOf(account.User);
    var zoneId = ExtractZoneId(text);

    if (zoneId.Length == 0 || !TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out _))
      return context.Reply($"{BotTexts.TimeZoneInvalid(language, text)}{BotTexts.TimeZoneListHint(language)}", replyMarkup: BuildKeyboard());

    try
    {
      await _mediator.Send(new UpdateUserPreferencesCommand(account.User.Id, null, zoneId), cancellationToken);
    }
    catch (Exception exception)
    {
      return context.Reply(exception.Message);
    }

    // Completed: "Назад" later skips this picker instead of showing it again.
    IsCompleted = true;

    return await NavigateToAsync<SettingsForm>(context, cancellationToken);
  }

  private static string ExtractZoneId(string text)
  {
    var value = text.Trim();

    if (value.StartsWith(CheckMark, StringComparison.Ordinal))
      value = value[CheckMark.Length..];

    var separator = value.IndexOf(OffsetSeparator, StringComparison.Ordinal);

    return separator > 0 ? value[..separator] : value;
  }
}
