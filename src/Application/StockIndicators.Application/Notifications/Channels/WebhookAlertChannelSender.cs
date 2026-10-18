using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Generic webhook channel sender. POSTs one JSON object per alert to the destination URL
/// stored in the output Target: trigger ids, instrument/indicator fields, the rendered text,
/// the signed chart image URL and the deep link to the subscription page.
/// A network-level failure (host unreachable) fails the rest of the batch at once.
/// </summary>
public class WebhookAlertChannelSender : IAlertChannelSender
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOptions<AlertOptions> _alertOptions;
  private readonly IOptions<TelegramBotOptions> _webOptions;
  private readonly ILogger _logger;

  public NotificationChannel Channel => NotificationChannel.Webhook;

  public WebhookAlertChannelSender(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertOptions> alertOptions,
    IOptions<TelegramBotOptions> webOptions,
    ILogger logger
  )
  {
    _httpClientFactory = httpClientFactory;
    _alertOptions = alertOptions;
    _webOptions = webOptions;
    _logger = logger.ForContext<WebhookAlertChannelSender>();
  }

  public async Task<IReadOnlyList<DeliveryOutcome>> SendAsync(AlertBatch batch, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(batch.Target))
      return FailedAll(batch, DeliveryErrors.WebhookUrlMissing);

    var client = _httpClientFactory.CreateClient(AlertHttpClients.Delivery);
    var outcomes = new List<DeliveryOutcome>(batch.Items.Count);

    foreach (var (index, item) in batch.Items.Select((item, index) => (index, item)))
    {
      using var content = new StringContent(JsonSerializer.Serialize(BuildPayload(batch, item)), Encoding.UTF8, "application/json");

      try
      {
        using var response = await client.PostAsync(batch.Target, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent));
          _logger.Information("Webhook delivered for User {UserId} ({Ticker} {Indicator})", batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
        }
        else
        {
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, $"Destination responded {(int)response.StatusCode} {response.ReasonPhrase}"));
          _logger.Warning("Webhook destination {StatusCode} for User {UserId} ({Ticker} {Indicator})", (int)response.StatusCode, batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, DeliveryErrors.Cancelled));
        break;
      }
      catch (Exception ex)
      {
        // Unreachable host, DNS or TLS trouble: retrying per item only burns the scheduler tick
        outcomes.AddRange(batch.Items.Skip(index).Select(other => new DeliveryOutcome(other.Source.TriggerId, DeliveryStatus.Failed, $"Destination is unreachable: {ex.Message}")));
        _logger.Warning(ex, "Webhook destination unreachable for User {UserId}", batch.UserId);
        return outcomes;
      }
    }

    return outcomes;
  }

  private object BuildPayload(AlertBatch batch, RenderedAlertItem item)
  {
    var source = item.Source;

    return new
    {
      triggerId = source.TriggerId,
      subscriptionId = source.SubscriptionId,
      ticker = source.InstrumentTicker,
      exchange = source.ExchangeName,
      indicator = source.IndicatorName,
      text = item.RenderedText,
      triggeredAt = source.TriggeredAt,
      language = batch.Language,
      imageUrl = AlertDeepLinks.BuildChartImageUrl(_alertOptions.Value, source.TriggerId),
      url = _webOptions.Value.InstanceUrlFor(AlertDeepLinks.SubscriptionPath(source.InstrumentTicker, source.SubscriptionId))
    };
  }

  private static IReadOnlyList<DeliveryOutcome> FailedAll(AlertBatch batch, string error) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];
}
