using Drenalol.Telegram.Bot.Forms.Localization;
using Telegram.Bot.Types;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Which language to answer a chat in: the linked account's preference wins; a chat that
/// is not linked yet speaks what /language stored for it; everyone else gets the default.
/// The framework asks this for its own texts (Back, pagination, errors); the domain forms
/// ask it through <c>FormBase.GetLanguageAsync</c> instead of threading the question
/// through their own resolvers.
/// </summary>
public class ChatLanguageResolver : IChatLanguageResolver
{
  private readonly IChatAccountResolver _chatAccountResolver;
  private readonly IChatLanguageStore _chatLanguageStore;

  public ChatLanguageResolver(
    IChatAccountResolver chatAccountResolver,
    IChatLanguageStore chatLanguageStore
  )
  {
    _chatAccountResolver = chatAccountResolver;
    _chatLanguageStore = chatLanguageStore;
  }

  public async Task<string> ResolveAsync(Chat chat, CancellationToken cancellationToken = default)
  {
    var account = await _chatAccountResolver.GetByChatIdAsync(chat.Id, cancellationToken);

    if (account is not null)
      return BotTexts.LanguageOf(account.User);

    return Configuration.Locale.UserLocale.NormalizeLanguage(
      await _chatLanguageStore.GetLanguageAsync(chat.Id, cancellationToken));
  }
}
