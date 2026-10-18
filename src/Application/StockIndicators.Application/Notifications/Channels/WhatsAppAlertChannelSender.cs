using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Providers;
using Serilog;
using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// WhatsApp channel sender over the Meta WhatsApp Cloud API. Every alert is delivered as an
/// approved utility template message (the template body carries one variable with the rendered
/// alert text). The destination phone (output Target, E.164) belongs to the user.
/// Until the Graph API credentials are configured the channel skips deliveries explicitly.
/// </summary>
public partial class WhatsAppAlertChannelSender : IAlertChannelSender
{
  private const int MaxErrorLength = 500;

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOptions<WhatsAppOptions> _options;
  private readonly ProviderSettingsSnapshot _settings;
  private readonly IConfiguration _configuration;
  private readonly ILogger _logger;

  public NotificationChannel Channel => NotificationChannel.WhatsApp;

  public WhatsAppAlertChannelSender(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppOptions> options,
    ProviderSettingsSnapshot settings,
    IConfiguration configuration,
    ILogger logger
  )
  {
    _httpClientFactory = httpClientFactory;
    _options = options;
    _settings = settings;
    _configuration = configuration;
    _logger = logger.ForContext<WhatsAppAlertChannelSender>();
  }

  [GeneratedRegex(@"^\+[1-9]\d{5,14}$")]
  private static partial Regex PhoneRegex();

  public async Task<IReadOnlyList<DeliveryOutcome>> SendAsync(AlertBatch batch, CancellationToken cancellationToken)
  {
    // WhatsApp is opt-in (the toggle defaults to off): keys alone do not turn it on
    if (!_settings.IsEnabled(_configuration, "whatsapp"))
    {
      _logger.Information("WhatsApp channel is disabled in settings, skipping {AlertCount} alerts", batch.Items.Count);
      return SkippedAll(batch, DeliveryErrors.ChannelUnavailable);
    }

    var options = _options.Value;

    if (!options.IsConfigured)
    {
      _logger.Warning("WhatsApp options are not configured, skipping {AlertCount} alerts", batch.Items.Count);
      return SkippedAll(batch, DeliveryErrors.WhatsAppNotConfigured);
    }

    var phone = NormalizePhone(batch.Target);

    if (phone is null)
      return FailedAll(batch, DeliveryErrors.WhatsAppNumberInvalid);

    var client = _httpClientFactory.CreateClient(AlertHttpClients.Delivery);
    var url = $"https://graph.facebook.com/{options.ApiVersion.TrimEnd('/')}/{options.PhoneNumberId}/messages";
    var outcomes = new List<DeliveryOutcome>(batch.Items.Count);

    foreach (var item in batch.Items)
    {
      using var request = new HttpRequestMessage(HttpMethod.Post, url);
      request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {options.AccessToken}");
      request.Content = new StringContent(
        JsonSerializer.Serialize(BuildPayload(phone, options, item.RenderedText)),
        Encoding.UTF8,
        "application/json");

      try
      {
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent));
          _logger.Information("WhatsApp delivered for User {UserId} ({Ticker} {Indicator})", batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
        }
        else
        {
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, Truncate($"WhatsApp API responded {(int)response.StatusCode}: {body}", MaxErrorLength)));
          _logger.Warning("WhatsApp API {StatusCode} for User {UserId} ({Ticker} {Indicator}): {Body}", (int)response.StatusCode, batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName, Truncate(body, MaxErrorLength));
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, DeliveryErrors.Cancelled));
        break;
      }
      catch (Exception ex)
      {
        outcomes.AddRange(FailRest(batch, item, $"WhatsApp API is unreachable: {ex.Message}"));
        _logger.Warning(ex, "WhatsApp API unreachable for User {UserId}", batch.UserId);
        return outcomes;
      }
    }

    return outcomes;
  }

  private static object BuildPayload(string phone, WhatsAppOptions options, string text) => new
  {
    messaging_product = "whatsapp",
    to = phone,
    type = "template",
    template = new
    {
      name = options.TemplateName,
      language = new { code = options.TemplateLanguage },
      components = new object[]
      {
        new
        {
          type = "body",
          parameters = new object[] { new { type = "text", text } }
        }
      }
    }
  };

  /// <summary>
  /// Strips formatting characters and accepts an international number with or without
  /// the plus; anything else is not E.164 and never delivers.
  /// </summary>
  public static string? NormalizePhone(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return null;

    var digits = raw.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Trim();

    if (!digits.StartsWith('+'))
      digits = $"+{digits}";

    return PhoneRegex().IsMatch(digits) ? digits : null;
  }

  private static IReadOnlyList<DeliveryOutcome> FailedAll(AlertBatch batch, string error) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  private static IReadOnlyList<DeliveryOutcome> SkippedAll(AlertBatch batch, string reason) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Skipped, reason))];

  private static IReadOnlyList<DeliveryOutcome> FailRest(AlertBatch batch, RenderedAlertItem from, string error) =>
    [.. batch.Items.SkipWhile(item => item != from).Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
