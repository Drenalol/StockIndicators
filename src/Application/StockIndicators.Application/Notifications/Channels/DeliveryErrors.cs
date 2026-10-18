namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Stable machine-readable codes persisted in <see cref="Domain.Models.Subscriptions.TriggerDelivery.Error"/>
/// for the known failure/skip reasons. The trigger history UI maps them to localized strings
/// (frontend i18n keys subscriptions.triggerHistory.errors.*); anything else stored here
/// (provider responses, exception text) is diagnostic free text and is shown to the user as-is.
/// Format: "delivery_error:&lt;snake_case&gt;", the prefix separates codes from free text of old rows.
/// </summary>
public static class DeliveryErrors
{
  public const string ChannelUnavailable = "delivery_error:channel_unavailable";
  public const string NoResult = "delivery_error:no_result";
  public const string Cancelled = "delivery_error:cancelled";

  public const string NoTelegramChat = "delivery_error:no_telegram_chat";
  public const string TelegramChatUnlinked = "delivery_error:telegram_chat_unlinked";
  public const string TelegramMerged = "delivery_error:telegram_merged";

  public const string PushTargetMissing = "delivery_error:push_target_missing";
  public const string PushSubscriptionGone = "delivery_error:push_subscription_gone";
  public const string PushNotConfigured = "delivery_error:push_not_configured";
  public const string PushEndpointExpired = "delivery_error:push_endpoint_expired";
  public const string PushRateLimited = "delivery_error:push_rate_limited";

  public const string WebhookUrlMissing = "delivery_error:webhook_url_missing";

  public const string DiscordUrlMissing = "delivery_error:discord_url_missing";
  public const string DiscordRateLimited = "delivery_error:discord_rate_limited";

  public const string WhatsAppNotConfigured = "delivery_error:whatsapp_not_configured";
  public const string WhatsAppNumberInvalid = "delivery_error:whatsapp_number_invalid";
}
