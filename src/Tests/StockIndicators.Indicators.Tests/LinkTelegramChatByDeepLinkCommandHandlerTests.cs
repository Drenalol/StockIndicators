using Moq;
using StockIndicators.Application.Account.LinkTelegramChatByDeepLink;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.Telegram;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class LinkTelegramChatByDeepLinkCommandHandlerTests
{
  private readonly Guid _userId = Guid.NewGuid();
  private readonly long _chatId = -100123;
  private readonly Mock<ITelegramLinkTokenStore> _tokenStoreMock = new();
  private readonly Mock<ITelegramChatRepository> _chatRepositoryMock = new();

  private LinkTelegramChatByDeepLinkCommandHandler CreateHandler(Guid? userId, TelegramChat? existing = null)
  {
    _tokenStoreMock
      .Setup(s => s.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(userId);

    _chatRepositoryMock
      .Setup(r => r.GetByChatIdAsync(_chatId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(existing);

    _chatRepositoryMock
      .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    return new LinkTelegramChatByDeepLinkCommandHandler(_tokenStoreMock.Object, _chatRepositoryMock.Object);
  }

  [Fact]
  public async Task Handle_CreatesChatForValidToken()
  {
    var handler = CreateHandler(_userId);

    var result = await handler.Handle(new LinkTelegramChatByDeepLinkCommand(_chatId, "token", "user"), CancellationToken.None);

    Assert.Equal(_chatId, result.ChatId);
    Assert.Equal("user", result.Username);
    _chatRepositoryMock.Verify(r => r.AddAsync(It.Is<TelegramChat>(c => c.UserId == _userId && c.ChatId == _chatId), It.IsAny<CancellationToken>()), Times.Once);
    _chatRepositoryMock.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Handle_BindsExistingUnownedChatWithoutAdding()
  {
    var chat = new TelegramChat { Id = Guid.NewGuid(), ChatId = _chatId, UserId = Guid.Empty };
    var handler = CreateHandler(_userId, chat);

    await handler.Handle(new LinkTelegramChatByDeepLinkCommand(_chatId, "token", null), CancellationToken.None);

    Assert.Equal(_userId, chat.UserId);
    _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<TelegramChat>(), It.IsAny<CancellationToken>()), Times.Never);
    _chatRepositoryMock.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Handle_UnlinksPreviousChatWhenLinkingANewOne()
  {
    // An account has at most one chat: the deep link replaces the chat linked before it
    var oldChat = new TelegramChat { Id = Guid.NewGuid(), ChatId = 555, UserId = _userId };

    var handler = CreateHandler(_userId);

    // registered after CreateHandler so the per-user stub wins over the default one
    _chatRepositoryMock
      .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
      .ReturnsAsync([oldChat]);

    await handler.Handle(new LinkTelegramChatByDeepLinkCommand(_chatId, "token", null), CancellationToken.None);

    _chatRepositoryMock.Verify(r => r.DeleteAsync(oldChat, It.IsAny<CancellationToken>()), Times.Once);
    _chatRepositoryMock.Verify(r => r.AddAsync(It.Is<TelegramChat>(c => c.ChatId == _chatId), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Handle_RejectsChatAlreadyOwnedByAnotherUser()
  {
    var chat = new TelegramChat { Id = Guid.NewGuid(), ChatId = _chatId, UserId = Guid.NewGuid() };
    var handler = CreateHandler(_userId, chat);

    // A distinct error so the bot says "already linked to another account", not "link expired"
    await Assert.ThrowsAsync<TelegramChatOwnedByAnotherAccountException>(() =>
      handler.Handle(new LinkTelegramChatByDeepLinkCommand(_chatId, "token", null), CancellationToken.None));

    _chatRepositoryMock.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Handle_RejectsInvalidToken()
  {
    var handler = CreateHandler(null);

    await Assert.ThrowsAsync<TelegramLinkTokenInvalidException>(() =>
      handler.Handle(new LinkTelegramChatByDeepLinkCommand(_chatId, "token", null), CancellationToken.None));

    _chatRepositoryMock.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
  }
}
