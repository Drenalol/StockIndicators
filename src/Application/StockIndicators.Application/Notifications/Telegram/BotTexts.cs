using System.Net;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Application.Notifications.Telegram;

/// <summary>
/// Thin accessors over <see cref="LocalizationCatalog"/> for the Telegram bot UI.
/// Unlinked chats (no account behind the chat) fall back to the default language.
/// New strings live in the catalog keyed by <see cref="LocalizationKey"/> — no per-language
/// switches here.
/// </summary>
public static class BotTexts
{
  public static string LanguageOf(User? user) =>
    UserLocale.NormalizeLanguage(user?.Language);

  public static string[] BackButtons() =>
    UserLocale.SupportedLanguages.Select(descriptor => BackButton(descriptor.Code)).Distinct().ToArray();

  public static string BackButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Back, language);

  public static string SubscriptionsButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Subscriptions, language);

  public static string IndicatorsButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Indicators, language);

  public static string InstrumentsButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Instruments, language);

  public static string SettingsButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Settings, language);

  public static string LinkThisChatButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.LinkThisChat, language);

  public static string ManageChatsButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.ManageChats, language);

  public static string AddSubscriptionButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.AddSubscription, language);

  public static string CreateSubscriptionButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.CreateSubscription, language);

  public static string EditSubscriptionButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.EditSubscription, language);

  public static string SubscribeButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.Subscribe, language);

  public static string PrevPageButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.PrevPage, language);

  public static string NextPageButton(string language) =>
    LocalizationCatalog.Get(LocalizationKey.NextPage, language);

  public static string IndicatorsListTitle(string language) =>
    LocalizationCatalog.Get(LocalizationKey.IndicatorsListTitle, language);

  public static string InstrumentsListTitle(string language, int page, int totalPages, string? search)
  {
    var pages = totalPages > 1 ? $" - {page}/{totalPages}" : string.Empty;

    if (!string.IsNullOrWhiteSpace(search))
      return LocalizationCatalog.GetFormatted(
        LocalizationKey.InstrumentsSearchResultsTitle, language,
        ("search", search), ("pages", pages));

    return LocalizationCatalog.GetFormatted(LocalizationKey.InstrumentsListTitle, language, ("pages", pages));
  }

  public static string NoInstruments(string language) =>
    LocalizationCatalog.Get(LocalizationKey.NoInstruments, language);

  public static string InstrumentsSearchHint(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentsSearchHint, language);

  public static string IndicatorParametersHeader(string language) =>
    LocalizationCatalog.Get(LocalizationKey.IndicatorParametersHeader, language);

  public static string IndicatorNoParameters(string language) =>
    LocalizationCatalog.Get(LocalizationKey.IndicatorNoParameters, language);

  public static string InstrumentFieldName(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentFieldName, language);

  public static string InstrumentFieldType(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentFieldType, language);

  public static string InstrumentFieldCurrency(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentFieldCurrency, language);

  public static string InstrumentFieldSector(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentFieldSector, language);

  public static string InstrumentFieldIndustry(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentFieldIndustry, language);

  public static string InstrumentFieldCountry(string language) =>
    LocalizationCatalog.Get(LocalizationKey.InstrumentFieldCountry, language);

  public static string SubscriptionFieldInstrument(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionFieldInstrument, language);

  public static string SubscriptionFieldIndicator(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionFieldIndicator, language);

  public static string SubscriptionFieldCooldown(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionFieldCooldown, language);

  public static string SubscriptionFieldStatus(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionFieldStatus, language);

  public static string SubscriptionActive(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionActive, language);

  public static string SubscriptionInactive(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionInactive, language);

  public static string SubscriptionPaused(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionPaused, language);

  public static string SubscriptionFieldParameters(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionFieldParameters, language);

  public static string SubscriptionFieldChannels(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionFieldChannels, language);

  /// <summary>
  /// /start welcome: who we are, what for and how to use the bot. No account
  /// internals (API keys) — those live in the web settings.
  /// </summary>
  public static string StartAbout(string language, string name)
  {
    var safeName = WebUtility.HtmlEncode(name);

    return LocalizationCatalog.GetFormatted(
      LocalizationKey.StartAbout, language,
      ("name", safeName),
      ("subscriptions", SubscriptionsButton(language)),
      ("add", AddSubscriptionButton(language)),
      ("indicators", IndicatorsButton(language)),
      ("instruments", InstrumentsButton(language)),
      ("settings", SettingsButton(language)));
  }

  public static string SettingsTitle(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SettingsTitle, language);

  public static string SettingsLanguageLabel(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SettingsLanguageLabel, language);

  public static string SettingsTimezoneLabel(string language) =>
    LocalizationCatalog.Get(LocalizationKey.TimeZoneListTitle, language);

  /// <summary>
  /// The language's own name as displayed in the current UI language
  /// (the native name from the <see cref="UserLocale"/> registry, localized via the catalog).
  /// </summary>
  public static string LanguageDisplayName(string language) =>
    LocalizationCatalog.Get(CurrentLanguageNameKey(language), language);

  /// <summary>
  /// Catalog key holding the display name of <paramref name="language"/>. The language
  /// code maps to a key by name convention ("LanguageName" + PascalCase code, region
  /// included: "pt-BR" -> LanguageNamePtBr), so a new language needs only its registry
  /// entry and catalog rows - no switch here.
  /// </summary>
  private static LocalizationKey CurrentLanguageNameKey(string language)
  {
    var normalized = UserLocale.NormalizeLanguage(language);
    var keyName = "LanguageName" + string.Concat(
      normalized.Split('-', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    return Enum.TryParse<LocalizationKey>(keyName, out var key) ? key : LocalizationKey.LanguageNameEn;
  }

  public static string LanguageListTitle(string language) =>
    LocalizationCatalog.Get(LocalizationKey.LanguageListTitle, language);

  public static string TimeZoneListTitle(string language) =>
    LocalizationCatalog.Get(LocalizationKey.TimeZoneListTitle, language);

  public static string TimeZoneListHint(string language) =>
    LocalizationCatalog.Get(LocalizationKey.TimeZoneListHint, language);

  public static string TimeZoneInvalid(string language, string value) =>
    LocalizationCatalog.GetFormatted(LocalizationKey.TimeZoneInvalid, language, ("value", value));

  /// <summary>
  /// First contact in an unlinked private chat: what the bot is and how the chat gets
  /// bound - with a one-time token from the instance's settings page (StartForm).
  /// </summary>
  public static string StartNotLinked(string language, string name, string linkButtonText) =>
    LocalizationCatalog.GetFormatted(
      LocalizationKey.StartNotLinked, language,
      ("name", WebUtility.HtmlEncode(name)),
      ("link", WebUtility.HtmlEncode(linkButtonText)));

  public static string LinkCommandUsage(string language) =>
    LocalizationCatalog.Get(LocalizationKey.LinkCommandUsage, language);


  public static string SettingsUnlinked(string language)
  {
    var linkButton = LinkThisChatButton(language);

    return LocalizationCatalog.GetFormatted(
      LocalizationKey.SettingsUnlinked, language, ("link", linkButton));
  }

  public static string LinkedChatsHeader(string language) =>
    LocalizationCatalog.Get(LocalizationKey.LinkedChatsHeader, language);

  public static string NoChats(string language) =>
    LocalizationCatalog.Get(LocalizationKey.NoChats, language);

  public static string ChatLine(string language, string label) =>
    LocalizationCatalog.GetFormatted(LocalizationKey.ChatLine, language, ("label", label));

  public static string ChatNotLinked(string language) =>
    LocalizationCatalog.Get(LocalizationKey.ChatNotLinked, language);

  public static string NoSubscriptions(string language) =>
    LocalizationCatalog.Get(LocalizationKey.NoSubscriptions, language);

  public static string TemporarilyUnavailable(string language) =>
    LocalizationCatalog.Get(LocalizationKey.TemporarilyUnavailable, language);

  public static string UnknownIndicator(string language) =>
    LocalizationCatalog.Get(LocalizationKey.UnknownIndicator, language);

  public static string SubscriptionNotFound(string language) =>
    LocalizationCatalog.Get(LocalizationKey.SubscriptionNotFound, language);

  public static string SubscriptionListTitle(string language, int page = 1, int totalPages = 1)
  {
    var pages = totalPages > 1 ? $" - {page}/{totalPages}" : string.Empty;

    return LocalizationCatalog.GetFormatted(LocalizationKey.SubscriptionListTitle, language, ("pages", pages));
  }

  public static string SubscriptionsSearchHint(string language, string? search) =>
    string.IsNullOrWhiteSpace(search)
      ? LocalizationCatalog.Get(LocalizationKey.SubscriptionsSearchHint, language)
      : LocalizationCatalog.GetFormatted(LocalizationKey.SubscriptionsFilterActive, language, ("filter", search));

  public static string ChatNotLinkedOrNoKeys(string language) =>
    LocalizationCatalog.Get(LocalizationKey.ChatNotLinkedOrNoKeys, language);

  public static string ChatLinkedToAnother(string language) =>
    LocalizationCatalog.Get(LocalizationKey.ChatLinkedToAnother, language);

  public static string GroupChatNotAllowed(string language) =>
    LocalizationCatalog.Get(LocalizationKey.GroupChatNotAllowed, language);

  /// <summary>
  /// Shown when a deep-link token has expired or was already used — the user must request
  /// a fresh link from the web settings.
  /// </summary>
  public static string LinkTokenInvalid(string language) =>
    LocalizationCatalog.Get(LocalizationKey.LinkTokenInvalid, language);

  public static string ChatLinkedConfirmation(string language, string name)
  {
    var safeName = WebUtility.HtmlEncode(name);

    return LocalizationCatalog.GetFormatted(
      LocalizationKey.ChatLinkedConfirmation, language, ("name", safeName));
  }

}
