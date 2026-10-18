using Moq;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Domain.Models.Telegram;
using StockIndicators.Domain.Models.Users;
using Xunit;

namespace StockIndicators.Indicators.Tests;

public class ChatAccountResolverTests
{
  private readonly Mock<ITelegramChatRepository> _chats = new();
  private readonly Mock<IUserRepository> _users = new();
  private readonly ChatAccountResolver _resolver;

  private readonly Guid _userId = Guid.NewGuid();

  public ChatAccountResolverTests()
  {
    _resolver = new ChatAccountResolver(_chats.Object, _users.Object);
  }

  private TelegramChat Chat(long chatId) =>
    new()
    {
      Id = Guid.NewGuid(),
      UserId = _userId,
      ChatId = chatId
    };

  [Fact]
  public async Task GetByChatId_ReturnsNullWhenChatIsNotLinked()
  {
    _chats.Setup(r => r.GetByChatIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync((TelegramChat?)null);

    Assert.Null(await _resolver.GetByChatIdAsync(42));
  }

  [Fact]
  public async Task GetByChatId_ReturnsUserAndChats()
  {
    var chat = Chat(42);
    var user = new User { Id = _userId, DisplayName = "Tester" };

    _chats.Setup(r => r.GetByChatIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(chat);
    _chats.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync([chat]);
    _users.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var account = await _resolver.GetByChatIdAsync(42);

    Assert.NotNull(account);
    Assert.Same(user, account.User);
    Assert.Single(account.Chats);
  }

  [Fact]
  public async Task GetByChatId_ReturnsNullWhenUserIsMissing()
  {
    var chat = Chat(42);

    _chats.Setup(r => r.GetByChatIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(chat);
    _users.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

    Assert.Null(await _resolver.GetByChatIdAsync(42));
  }
}
