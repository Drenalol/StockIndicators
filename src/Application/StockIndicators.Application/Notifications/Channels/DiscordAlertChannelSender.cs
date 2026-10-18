using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.Notifications;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Discord channel sender. POSTs one rich embed per alert to the channel webhook URL
/// stored in the output Target (webhooks are created by the user in the Discord channel
/// settings; no bot or OAuth is involved). 429 from Discord fails the rest of the batch.
/// </summary>
public class DiscordAlertChannelSender : IAlertChannelSender
{
  private const int MaxTitleLength = 256;
  private const int MaxDescriptionLength = 2048;

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOptions<AlertOptions> _alertOptions;
  private readonly IOptions<TelegramBotOptions> _webOptions;
  private readonly ILogger _logger;

  public NotificationChannel Channel => NotificationChannel.Discord;

  public DiscordAlertChannelSender(
    IHttpClientFactory httpClientFactory,
    IOptions<AlertOptions> alertOptions,
    IOptions<TelegramBotOptions> webOptions,
    ILogger logger
  )
  {
    _httpClientFactory = httpClientFactory;
    _alertOptions = alertOptions;
    _webOptions = webOptions;
    _logger = logger.ForContext<DiscordAlertChannelSender>();
  }

  public async Task<IReadOnlyList<DeliveryOutcome>> SendAsync(AlertBatch batch, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(batch.Target))
      return FailedAll(batch, DeliveryErrors.DiscordUrlMissing);

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
          _logger.Information("Discord delivered for User {UserId} ({Ticker} {Indicator})", batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
        }
        else if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
          outcomes.AddRange(FailRest(batch, index, DeliveryErrors.DiscordRateLimited));
          _logger.Warning("Discord webhook rate-limited for User {UserId}, stopping the batch", batch.UserId);
          return outcomes;
        }
        else
        {
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, $"Discord responded {(int)response.StatusCode} {response.ReasonPhrase}"));
          _logger.Warning("Discord webhook {StatusCode} for User {UserId} ({Ticker} {Indicator})", (int)response.StatusCode, batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, DeliveryErrors.Cancelled));
        break;
      }
      catch (Exception ex)
      {
        outcomes.AddRange(FailRest(batch, index, $"Discord is unreachable: {ex.Message}"));
        _logger.Warning(ex, "Discord webhook unreachable for User {UserId}", batch.UserId);
        return outcomes;
      }
    }

    return outcomes;
  }

  private object BuildPayload(AlertBatch batch, RenderedAlertItem item)
  {
    var source = item.Source;

    var embed = new Dictionary<string, object?>
    {
      ["title"] = Truncate($"{source.InstrumentTicker} - {source.IndicatorName}", MaxTitleLength),
      ["description"] = Truncate(item.RenderedText, MaxDescriptionLength),
      ["timestamp"] = source.TriggeredAt.ToString("O"),
      ["url"] = _webOptions.Value.InstanceUrlFor(AlertDeepLinks.SubscriptionPath(source.InstrumentTicker, source.SubscriptionId)),
      ["color"] = 0x4f9cff
    };

    var image = AlertDeepLinks.BuildChartImageUrl(_alertOptions.Value, source.TriggerId);

    if (image is not null)
      embed["image"] = new { url = image };

    return new { username = "Stock Indicators", embeds = new[] { embed } };
  }

  private static IReadOnlyList<DeliveryOutcome> FailedAll(AlertBatch batch, string error) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  private static IReadOnlyList<DeliveryOutcome> FailRest(AlertBatch batch, int fromIndex, string error) =>
    [.. batch.Items.Skip(fromIndex).Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..(maxLength - 1)]}…";
}
