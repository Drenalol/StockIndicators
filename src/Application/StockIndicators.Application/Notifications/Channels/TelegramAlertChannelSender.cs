using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace StockIndicators.Application.Notifications.Channels;

/// <summary>
/// Telegram channel sender. Deduplicates alerts by (InstrumentTicker, IndicatorName) keeping the latest,
/// and splits into chunks respecting the Telegram 4096 character limit.
/// With the bot's link buttons on, every message carries an inline keyboard with a deep link
/// to the subscription page (the same path the web push payload uses in data.path); with them
/// off, alerts go out as plain text.
/// </summary>
public class TelegramAlertChannelSender : IAlertChannelSender
{
  private const int MaxMessageLength = 4096;
  private const int MaxCaptionLength = 1024;

  /// <summary>Inline keyboards cap at 100 buttons; two per alert row.</summary>
  private const int MaxKeyboardRows = 40;

  private readonly ITelegramBotClient _telegramBotClient;
  private readonly ITelegramChatRepository _telegramChatRepository;
  private readonly IOptions<TelegramBotOptions> _botOptions;
  private readonly ILogger _logger;

  public NotificationChannel Channel => NotificationChannel.Telegram;

  public TelegramAlertChannelSender(
    ITelegramBotClient telegramBotClient,
    ITelegramChatRepository telegramChatRepository,
    IOptions<TelegramBotOptions> botOptions,
    ILogger logger
  )
  {
    _telegramBotClient = telegramBotClient;
    _telegramChatRepository = telegramChatRepository;
    _botOptions = botOptions;
    _logger = logger.ForContext<TelegramAlertChannelSender>();
  }

  public async Task<IReadOnlyList<DeliveryOutcome>> SendAsync(AlertBatch batch, CancellationToken cancellationToken)
  {
    // Delivery target: the chat resolved by the dispatcher. The stored output ChatId may be
    // stale — an account has exactly one chat, and linking a new chat unlinks the previous
    // one, so a batch addressed at an unlinked/foreign chat is rerouted to the account's
    // current chat, and batches without an explicit chat go straight to it.
    long? chatId = batch.ChatId;
    var currentChat = (await _telegramChatRepository.GetByUserIdAsync(batch.UserId, cancellationToken)).FirstOrDefault();

    if (chatId is null || chatId == currentChat?.ChatId)
    {
      if (currentChat is null)
      {
        _logger.Warning("User {UserId} has no linked Telegram chat, skipping alerts", batch.UserId);
        return FailedAll(batch, DeliveryErrors.NoTelegramChat);
      }

      if (chatId is null)
        _logger.Warning("Telegram batch for User {UserId} has no explicit chat, falling back to chat {FallbackChatId}", batch.UserId, currentChat.ChatId);

      chatId = currentChat.ChatId;
    }
    else
    {
      var chat = await _telegramChatRepository.GetByChatIdAsync(chatId.Value, cancellationToken);

      if (chat is null || chat.UserId != batch.UserId)
      {
        if (currentChat is not null)
        {
          _logger.Warning(
            "Telegram chat {ChatId} for User {UserId} is unlinked, rerouting alerts to the account's current chat {CurrentChatId}",
            chatId.Value, batch.UserId, currentChat.ChatId);
          chatId = currentChat.ChatId;
        }
        else
        {
          _logger.Warning("Telegram chat {ChatId} for User {UserId} is unlinked and the account has no other chat, skipping alerts", chatId.Value, batch.UserId);
          return FailedAll(batch, DeliveryErrors.TelegramChatUnlinked);
        }
      }
    }

    var outcomes = new List<DeliveryOutcome>(batch.Items.Count);
    var deduplicated = DeduplicateAlerts(batch.Items);

    // Alerts with a rendered chart go out as photos (chart + caption), the rest as plain text
    var imageItems = deduplicated.Where(item => item.Source.ImagePng is { Length: > 0 }).ToList();
    var textItems = deduplicated.Except(imageItems).ToList();

    foreach (var superseded in batch.Items.Except(deduplicated))
      outcomes.Add(new DeliveryOutcome(superseded.Source.TriggerId, DeliveryStatus.Skipped, DeliveryErrors.TelegramMerged));

    foreach (var item in imageItems)
    {
      if (item.Source.ImagePng is not { Length: > 0 } png)
        continue;

      try
      {
        using var photo = new MemoryStream(png, writable: false);

        await _telegramBotClient.SendPhoto(
          chatId: new ChatId(chatId.Value),
          photo: photo,
          caption: Truncate(item.RenderedText, MaxCaptionLength),
          parseMode: ParseMode.Html,
          disableNotification: false,
          replyMarkup: BuildKeyboard(batch.Language, [item.Source]),
          cancellationToken: cancellationToken
        );

        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent));
        _logger.Information("Sent photo to chat {ChatId} of User {UserId} ({Ticker} {Indicator})", chatId.Value, batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
      }
      catch (Exception ex)
      {
        outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, ex.Message));
        _logger.Warning(ex, "Failed to send photo to chat {ChatId} of User {UserId} ({Ticker} {Indicator})", chatId.Value, batch.UserId, item.Source.InstrumentTicker, item.Source.IndicatorName);
      }
    }

    var textChunks = SplitItems(textItems, batch.Language);

    for (var chunkIndex = 0; chunkIndex < textChunks.Count; chunkIndex++)
    {
      var chunk = textChunks[chunkIndex];
      var text = chunkIndex == 0 ? BuildMessage(chunk, batch.Language) : string.Join("\n", chunk.Select(i => i.RenderedText));

      try
      {
        await _telegramBotClient.SendMessage(
          chatId: new ChatId(chatId.Value),
          text: text,
          parseMode: ParseMode.Html,
          disableNotification: false,
          replyMarkup: BuildKeyboard(batch.Language, [.. chunk.Select(i => i.Source)]),
          cancellationToken: cancellationToken
        );

        foreach (var item in chunk)
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Sent));

        _logger.Information("Sent batch to chat {ChatId} of User {UserId}: {Length} chars", chatId.Value, batch.UserId, chunk.Count);
      }
      catch (Exception ex)
      {
        foreach (var item in chunk)
          outcomes.Add(new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, ex.Message));

        _logger.Warning(ex, "Failed to send batch to chat {ChatId} of User {UserId}", chatId.Value, batch.UserId);
      }
    }

    return outcomes;
  }

  /// <summary>
  /// One button per alert: opens the subscription page of the instance (history lives on
  /// that page, so the alerts do not carry a second button). A plain url button - the
  /// instance is a website, there is no mini-app shell around it. Null when the bot carries
  /// no links: the alert then goes out as plain text instead of failing on a URL Telegram
  /// refuses.
  /// </summary>
  private InlineKeyboardMarkup? BuildKeyboard(string language, IReadOnlyCollection<AlertItem> items)
  {
    if (!_botOptions.Value.CanSendWebLinks)
      return null;

    var rows = items
      .Take(MaxKeyboardRows)
      .Select(item => new[]
      {
        InlineKeyboardButton.WithUrl(AlertTexts.TelegramOpenButton(language), BuildWebUrl(item))
      })
      .ToList();

    return new InlineKeyboardMarkup(rows);
  }

  private string BuildWebUrl(AlertItem item) =>
    _botOptions.Value.InstanceUrlFor(AlertDeepLinks.SubscriptionPath(item.InstrumentTicker, item.SubscriptionId));

  private static IReadOnlyList<DeliveryOutcome> FailedAll(AlertBatch batch, string error) =>
    [.. batch.Items.Select(item => new DeliveryOutcome(item.Source.TriggerId, DeliveryStatus.Failed, error))];

  /// <summary>
  /// Groups alerts by (InstrumentTicker, IndicatorName) and keeps only the latest one per group.
  /// Items are ordered chronologically, so Last() returns the most recent trigger.
  /// </summary>
  private static List<RenderedAlertItem> DeduplicateAlerts(IReadOnlyList<RenderedAlertItem> items)
  {
    var grouped = items.GroupBy(
      a => (a.Source.InstrumentTicker, a.Source.IndicatorName),
      a => a,
      (key, group) => group.Last());

    return grouped.ToList();
  }

  private static string BuildMessage(List<RenderedAlertItem> items, string language)
  {
    var parts = items.Select(a => a.RenderedText);
    return $"{AlertTexts.TelegramHeader(language)}\n\n{string.Join("\n", parts)}";
  }

  /// <summary>
  /// Splits text items into chunks of at most MaxMessageLength characters, breaking at line
  /// boundaries. The header rides on the first chunk only. Item-level chunking keeps delivery
  /// outcomes exact: every item belongs to exactly one chunk.
  /// </summary>
  private static List<List<RenderedAlertItem>> SplitItems(IReadOnlyList<RenderedAlertItem> items, string language)
  {
    var chunks = new List<List<RenderedAlertItem>>();

    if (items.Count == 0)
      return chunks;

    var headerLength = AlertTexts.TelegramHeader(language).Length + 2;
    var current = new List<RenderedAlertItem>();
    var currentLength = headerLength;

    foreach (var item in items)
    {
      var lineLength = item.RenderedText.Length + 1;

      if (current.Count > 0 && currentLength + lineLength > MaxMessageLength)
      {
        chunks.Add(current);
        current = [];
        currentLength = 0;
      }

      current.Add(item);
      currentLength += lineLength;
    }

    if (current.Count > 0)
      chunks.Add(current);

    return chunks;
  }

  private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..(maxLength - 1)]}…";
}
