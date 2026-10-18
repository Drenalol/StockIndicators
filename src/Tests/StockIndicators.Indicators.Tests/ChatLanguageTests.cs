using Drenalol.Telegram.Bot.Forms;
using Microsoft.Extensions.Options;
using Moq;
using StockIndicators.Application.Notifications.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;
using User = StockIndicators.Domain.Models.Users.User;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The "/language" picker and the chat language it leaves behind: an unlinked chat has no
/// user row, so the choice lives per chat and steers every unlinked answer - including the
/// "how to link me" instructions the start message gives.
/// </summary>
public class ChatLanguageTests
{
  private const long ChatId = 42424242;

  private sealed class ChatLanguages : IChatLanguageStore
  {
    private readonly Dictionary<long, string> _values = new();

    public string? ValueOf(long chatId) => _values.GetValueOrDefault(chatId);

    public Task<string?> GetLanguageAsync(long chatId, CancellationToken cancellationToken = default) =>
      Task.FromResult(ValueOf(chatId));

    public Task SetLanguageAsync(long chatId, string language, CancellationToken cancellationToken = default)
    {
      _values[chatId] = language;

      return Task.CompletedTask;
    }

    public Task ClearAsync(long chatId, CancellationToken cancellationToken = default)
    {
      _values.Remove(chatId);

      return Task.CompletedTask;
    }
  }

  private readonly ChatLanguages _store = new();
  private readonly Mock<IChatAccountResolver> _accounts = new();
  private readonly StartForm _startForm;
  private readonly ChatLanguageForm _languageForm;

  public ChatLanguageTests()
  {
    var options = Options.Create(new TelegramBotOptions { InstanceUrl = "https://example.com", IncludeWebLinks = true });

    _startForm = new StartForm(_accounts.Object, _store, null!, options);
    _languageForm = new ChatLanguageForm(_accounts.Object, _store, null!);
  }

  private static MessageContext Message(string text) =>
    new(new Chat { Id = ChatId, FirstName = "Alice", Type = ChatType.Private }, text);

  private void Linked(string language)
  {
    var user = new User { Id = Guid.NewGuid(), Language = language };

    _accounts
      .Setup(resolver => resolver.GetByChatIdAsync(ChatId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ChatAccount(user, []));
  }

  [Fact]
  public async Task UnlinkedStartExplainsTheTokenLinkingWithAButtonToTheSettings()
  {
    var context = await _startForm.RenderAsync(Message("/start"), CancellationToken.None);

    // No account to create and nothing to consent to: the chat is bound with a token from
    // the settings page, and the start message says exactly that
    Assert.DoesNotContain("Terms", context.ReplyText);
    Assert.Contains("/link", context.ReplyText);

    var keyboard = Assert.IsType<InlineKeyboardMarkup>(context.ReplyMarkup);
    var button = keyboard.InlineKeyboard.SelectMany(row => row).Single();

    Assert.Equal("https://example.com/dashboard/settings", button.Url);
  }

  [Fact]
  public async Task StartWithoutLinkButtonsCarriesNoKeyboardAtAll()
  {
    // links off (or an address Telegram refuses) must leave the bot with text only: a message
    // carrying a rejected URL is refused as a whole, so the chat would get nothing
    foreach (var options in new[]
    {
      new TelegramBotOptions { InstanceUrl = "https://example.com" },
      new TelegramBotOptions { InstanceUrl = "https://localhost:5051", IncludeWebLinks = true },
    })
    {
      var form = new StartForm(_accounts.Object, _store, null!, Options.Create(options));

      var context = await form.RenderAsync(Message("/start"), CancellationToken.None);

      Assert.Null(context.ReplyMarkup);
      Assert.Contains("/link", context.ReplyText);
    }
  }

  [Fact]
  public async Task AChosenChatLanguageSteersTheStartInstructions()
  {
    await _store.SetLanguageAsync(ChatId, "ru");

    var context = await _startForm.RenderAsync(Message("/start"), CancellationToken.None);

    Assert.Contains("ещё не привязан", context.ReplyText);

    var keyboard = Assert.IsType<InlineKeyboardMarkup>(context.ReplyMarkup);

    Assert.Equal("Привязать этот чат", keyboard.InlineKeyboard.SelectMany(row => row).Single().Text);
  }

  [Fact]
  public async Task ALinkedChatAnswersInItsUserLanguageAndCarriesTheMenuKeyboard()
  {
    await _store.SetLanguageAsync(ChatId, "ru");
    Linked("en");

    var context = await _startForm.RenderAsync(Message("/start"), CancellationToken.None);

    Assert.IsType<ReplyKeyboardMarkup>(context.ReplyMarkup);
    Assert.DoesNotContain("не привязан", context.ReplyText);
  }

  [Fact]
  public async Task ThePickerListsEverySupportedLanguageOfAnUnlinkedChat()
  {
    var context = await _languageForm.RenderAsync(Message("/language"), CancellationToken.None);

    // The list (not the start message); selecting a language stores it per chat, which is
    // what the ru-start test above then reads back
    Assert.IsType<ReplyKeyboardMarkup>(context.ReplyMarkup);
    Assert.Equal("Bot interface language", context.ReplyText);
  }

}
