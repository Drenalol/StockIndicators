using Moq;
using Serilog.Core;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.Telegram;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class TelegramAlertChannelSenderTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly Mock<ITelegramBotClient> _botMock = new();
  private readonly Mock<ITelegramChatRepository> _chatRepositoryMock = new();

  public TelegramAlertChannelSenderTests()
  {
    _chatRepositoryMock
      .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    // _userId has its current chat 42; every other user has no linked chat
    _chatRepositoryMock
      .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync([new TelegramChat { Id = Guid.NewGuid(), UserId = _userId, ChatId = 42 }]);

    _chatRepositoryMock
      .Setup(r => r.GetByChatIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((TelegramChat?)null);

    _botMock
      .Setup(c => c.SendRequest<Message>(It.IsAny<SendPhotoRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Message());

    _botMock
      .Setup(c => c.SendRequest<Message>(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Message());
  }

  private TelegramAlertChannelSender CreateSender(bool includeWebLinks = true) =>
    new(
      _botMock.Object,
      _chatRepositoryMock.Object,
      Microsoft.Extensions.Options.Options.Create(new TelegramBotOptions { Token = "t", InstanceUrl = "https://example.test", IncludeWebLinks = includeWebLinks }),
      Logger.None);

  private static RenderedAlertItem Item(string ticker, string indicator, string text, byte[]? imagePng) =>
    new(
      new AlertItem(Guid.NewGuid(), Guid.NewGuid(), ticker, null, indicator, text, DateTime.UtcNow, Guid.NewGuid(), imagePng),
      text
    );

  [Fact]
  public async Task SendAsync_SendsPhotoForAlertsWithImageAndTextForTheRest()
  {
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, null,
    [
      Item("AAPL", "SMA", "MA up", [1, 2, 3]),
      Item("MSFT", "EMA", "EMA down", null)
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendPhotoRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SendAsync_SendsTextOnlyWhenNoAlertHasImage()
  {
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, null,
    [
      Item("AAPL", "SMA", "MA up", null),
      Item("MSFT", "EMA", "EMA down", null)
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendPhotoRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SendAsync_LinksAlertsToTheirSubscriptionPage()
  {
    await CreateSender().SendAsync(TextBatch(), CancellationToken.None);

    _botMock.Verify(c => c.SendRequest<Message>(
      It.Is<SendMessageRequest>(request => LinksToSubscriptionPage(request)),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task SendAsync_SendsAlertsWithoutAKeyboardWhenLinkButtonsAreOff()
  {
    // a URL Telegram refuses takes the whole message down, so an instance the bot may not
    // link to leaves the alert as plain text instead of failing
    await CreateSender(includeWebLinks: false).SendAsync(TextBatch(), CancellationToken.None);

    _botMock.Verify(c => c.SendRequest<Message>(
      It.Is<SendMessageRequest>(request => WithoutKeyboard(request)),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  private AlertBatch TextBatch() =>
    new(NotificationChannel.Telegram, _userId, null, null, [Item("AAPL", "SMA", "MA up", null)]);

  // Moq matches on expression trees, so the checks live in methods rather than inline patterns
  private static bool LinksToSubscriptionPage(SendMessageRequest request) =>
    ((request.ReplyMarkup as InlineKeyboardMarkup)?.InlineKeyboard.SelectMany(row => row).SingleOrDefault())
    ?.Url?.StartsWith("https://example.test/dashboard/subscriptions/AAPL/") == true;

  private static bool WithoutKeyboard(SendMessageRequest request) => request.ReplyMarkup == null;

  [Fact]
  public async Task SendAsync_SendsOnlyPhotosWhenEveryAlertHasImage()
  {
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, null,
    [
      Item("AAPL", "SMA", "MA up", [1, 2, 3])
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendPhotoRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SendAsync_UsesExplicitChatIdWhenChatIsLinked()
  {
    _chatRepositoryMock
      .Setup(r => r.GetByChatIdAsync(777, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TelegramChat { Id = Guid.NewGuid(), UserId = _userId, ChatId = 777 });

    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, 777,
    [
      Item("AAPL", "SMA", "MA up", null)
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(
      c => c.SendRequest<Message>(It.Is<SendMessageRequest>(r => r.ChatId.Identifier == 777), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task SendAsync_ReroutesToCurrentChatWhenExplicitChatIsUnlinked()
  {
    // 777 is not linked to _userId anymore (default GetByChatIdAsync stub returns null),
    // but the account has its current chat 42 - alerts go there instead of failing
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, 777,
    [
      Item("AAPL", "SMA", "MA up", [1, 2, 3]),
      Item("MSFT", "EMA", "EMA down", null)
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(
      c => c.SendRequest<Message>(It.Is<SendPhotoRequest>(r => r.ChatId.Identifier == 42), It.IsAny<CancellationToken>()),
      Times.Once);
    _botMock.Verify(
      c => c.SendRequest<Message>(It.Is<SendMessageRequest>(r => r.ChatId.Identifier == 42), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task SendAsync_ReroutesToCurrentChatWhenExplicitChatBelongsToAnotherUser()
  {
    _chatRepositoryMock
      .Setup(r => r.GetByChatIdAsync(777, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TelegramChat { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ChatId = 777 });

    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, 777,
    [
      Item("AAPL", "SMA", "MA up", null)
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(
      c => c.SendRequest<Message>(It.Is<SendMessageRequest>(r => r.ChatId.Identifier == 42), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task SendAsync_SkipsBatchWhenUserHasNoLinkedChat()
  {
    var batch = new AlertBatch(NotificationChannel.Telegram, Guid.NewGuid(), null, null,
    [
      Item("AAPL", "SMA", "MA up", [1, 2, 3])
    ]);

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendPhotoRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    _botMock.Verify(c => c.SendRequest<Message>(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SendAsync_ReportsSentForEveryDeliveredItem()
  {
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, null,
    [
      Item("AAPL", "SMA", "MA up", [1, 2, 3]),
      Item("MSFT", "EMA", "EMA down", null)
    ]);

    var outcomes = await CreateSender().SendAsync(batch, CancellationToken.None);

    Assert.Equal(2, outcomes.Count);
    Assert.All(outcomes, outcome => Assert.Equal(DeliveryStatus.Sent, outcome.Status));
    Assert.Equal(
      batch.Items.Select(i => i.Source.TriggerId).OrderBy(g => g),
      outcomes.Select(o => o.TriggerId).OrderBy(g => g));
  }

  [Fact]
  public async Task SendAsync_ReportsSkippedForSupersededItems()
  {
    // same (ticker, indicator): only the newest goes out, the older one is superseded
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, null,
    [
      Item("AAPL", "SMA", "older", null),
      Item("AAPL", "SMA", "newer", null)
    ]);

    var outcomes = await CreateSender().SendAsync(batch, CancellationToken.None);

    var newer = batch.Items[1].Source.TriggerId;
    var older = batch.Items[0].Source.TriggerId;

    Assert.Equal(DeliveryStatus.Sent, outcomes.Single(o => o.TriggerId == newer).Status);
    Assert.Equal(DeliveryStatus.Skipped, outcomes.Single(o => o.TriggerId == older).Status);
  }

  [Fact]
  public async Task SendAsync_ReportsFailedForAllWhenChatIsUnlinkedAndAccountHasNoOtherChat()
  {
    // a fresh user: 777 resolves to no chat and the account itself has no linked chat
    var batch = new AlertBatch(NotificationChannel.Telegram, Guid.NewGuid(), null, 777,
    [
      Item("AAPL", "SMA", "MA up", null)
    ]);

    var outcomes = await CreateSender().SendAsync(batch, CancellationToken.None);

    var outcome = Assert.Single(outcomes);
    Assert.Equal(DeliveryStatus.Failed, outcome.Status);
    Assert.NotNull(outcome.Error);
  }

  [Fact]
  public async Task SendAsync_AttachesInlineKeyboardWithSubscriptionLink()
  {
    _chatRepositoryMock
      .Setup(r => r.GetByChatIdAsync(777, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new TelegramChat { Id = Guid.NewGuid(), UserId = _userId, ChatId = 777 });

    var item = Item("AAPL", "SMA", "MA up", null);
    var batch = new AlertBatch(NotificationChannel.Telegram, _userId, null, 777, [item], "en");

    await CreateSender().SendAsync(batch, CancellationToken.None);

    _botMock.Verify(
      c => c.SendRequest<Message>(
        It.Is<SendMessageRequest>(r =>
          ((Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)r.ReplyMarkup!).InlineKeyboard.Single().Single().Url!
            .StartsWith("https://example.test/dashboard/subscriptions/AAPL/")),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }
}
