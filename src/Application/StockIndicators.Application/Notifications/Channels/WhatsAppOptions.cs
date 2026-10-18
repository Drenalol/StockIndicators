namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// WhatsApp Cloud API (Meta) credentials for the alert template sender. Until all
/// required values are configured the WhatsApp channel reports deliveries as skipped.
/// </summary>
public class WhatsAppOptions
{
  /// <summary>Permanent Graph API access token of the WhatsApp Business tech provider.</summary>
  public string AccessToken { get; set; } = string.Empty;

  /// <summary>Phone number id (not the phone number itself) issued by Meta.</summary>
  public string PhoneNumberId { get; set; } = string.Empty;

  /// <summary>
  /// Approved utility template name. The template body must contain a single
  /// variable ({{1}}) that receives the rendered alert text.
  /// </summary>
  public string TemplateName { get; set; } = string.Empty;

  /// <summary>
  /// Template language code sent to Meta verbatim. Meta matches approved
  /// template languages strictly, so the user's UI language is not used here.
  /// </summary>
  public string TemplateLanguage { get; set; } = "en";

  /// <summary>Graph API version segment ("v23.0").</summary>
  public string ApiVersion { get; set; } = "v23.0";

  public bool IsConfigured =>
    !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(PhoneNumberId) && !string.IsNullOrEmpty(TemplateName);
}
