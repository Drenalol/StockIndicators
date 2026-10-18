using Drenalol.Telegram.Bot.Forms;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using StockIndicators.Application.Indicators.GetIndicatorSchema;
using StockIndicators.Application.Instruments.GetInstrument;
using StockIndicators.Application.Notifications.Telegram;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Users;
using Xunit;

namespace StockIndicators.Indicators.Tests;

/// <summary>
/// The bot framework re-renders a form restored from the chat's navigation history with
/// the text that triggered the re-entry - a "Назад" press, not the forwarded entity id.
/// The reported crash was exactly that: "Назад" reaching the subscription detail and
/// Guid.Parse answering the chat with "Unrecognized Guid format.". The detail forms must
/// replay the entity they were opened with and never leak a raw .NET message.
/// </summary>
public class TelegramDetailFormRerenderTests
{
  private const long ChatId = 42;

  private readonly Guid _userId = Guid.NewGuid();
  private readonly User _user = new() { DisplayName = "Tester" };

  private readonly Mock<IMediator> _mediator = new();
  private readonly Mock<IChatAccountResolver> _accounts = new();

  public TelegramDetailFormRerenderTests()
  {
    _accounts
      .Setup(r => r.GetByChatIdAsync(ChatId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ChatAccount(_user, []));
  }

  private string Language => BotTexts.LanguageOf(_user);

  private static MessageContext Context(string text) =>
    new(new Telegram.Bot.Types.Chat { Id = ChatId }, text);

  [Fact]
  public async Task SubscriptionForm_Rerenders_The_Same_Subscription_When_Back_Press_Reenters_It()
  {
    var subscriptionId = Guid.NewGuid();
    var queried = new List<Guid>();

    _mediator
      .Setup(m => m.Send(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((GetSubscriptionQuery query, CancellationToken _) =>
      {
        queried.Add(query.SubscriptionId);

        return new SubscriptionDto { Id = query.SubscriptionId, UserId = _userId };
      });

    var form = new SubscriptionForm(_mediator.Object, _accounts.Object, Options.Create(new TelegramBotOptions()));

    var first = await form.RenderAsync(Context(subscriptionId.ToString()), CancellationToken.None);
    var back = await form.RenderAsync(Context("Назад"), CancellationToken.None);

    Assert.DoesNotContain("Unrecognized Guid format", back.ReplyText);
    Assert.Equal(first.ReplyText, back.ReplyText);
    Assert.Equal(new[] { subscriptionId, subscriptionId }, queried);
  }

  [Fact]
  public async Task SubscriptionForm_Answers_A_Text_Without_Id_With_Localized_NotFound()
  {
    var form = new SubscriptionForm(_mediator.Object, _accounts.Object, Options.Create(new TelegramBotOptions()));

    var reply = await form.RenderAsync(Context("Назад"), CancellationToken.None);

    Assert.Equal(BotTexts.SubscriptionNotFound(Language), reply.ReplyText);
    _mediator.Verify(m => m.Send(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task SubscriptionForm_Answers_A_Missing_Subscription_With_Localized_NotFound()
  {
    _mediator
      .Setup(m => m.Send(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new SubscriptionNotFoundException());

    var form = new SubscriptionForm(_mediator.Object, _accounts.Object, Options.Create(new TelegramBotOptions()));

    var reply = await form.RenderAsync(Context(Guid.NewGuid().ToString()), CancellationToken.None);

    Assert.Equal(BotTexts.SubscriptionNotFound(Language), reply.ReplyText);
  }

  [Fact]
  public async Task IndicatorDetailForm_Rerenders_The_Same_Indicator_When_Back_Press_Reenters_It()
  {
    var queried = new List<string>();

    _mediator
      .Setup(m => m.Send(It.IsAny<GetIndicatorSchemaQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((GetIndicatorSchemaQuery query, CancellationToken _) =>
      {
        queried.Add(query.Indicator.ToString());

        return new IndicatorSchemaDto(
          query.Indicator.ToString(),
          "description",
          new Dictionary<string, string>(),
          new Dictionary<string, string>(),
          new Dictionary<string, string>(),
          new Dictionary<string, string[]>());
      });

    var form = new IndicatorDetailForm(_mediator.Object, _accounts.Object, Options.Create(new TelegramBotOptions()));

    var first = await form.RenderAsync(Context("RSI"), CancellationToken.None);
    var back = await form.RenderAsync(Context("Назад"), CancellationToken.None);

    Assert.Contains("RSI", first.ReplyText);
    Assert.Equal(first.ReplyText, back.ReplyText);
    Assert.Equal(new[] { Indicator.RSI.ToString(), Indicator.RSI.ToString() }, queried);
  }

  [Fact]
  public async Task IndicatorDetailForm_Answers_A_Text_Without_Indicator_With_Unknown_Indicator()
  {
    var form = new IndicatorDetailForm(_mediator.Object, _accounts.Object, Options.Create(new TelegramBotOptions()));

    var reply = await form.RenderAsync(Context("Назад"), CancellationToken.None);

    Assert.Equal(BotTexts.UnknownIndicator(Language), reply.ReplyText);
    _mediator.Verify(m => m.Send(It.IsAny<GetIndicatorSchemaQuery>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task InstrumentDetailForm_Rerenders_The_Same_Instrument_When_Back_Press_Reenters_It()
  {
    var queried = new List<string>();

    _mediator
      .Setup(m => m.Send(It.IsAny<GetInstrumentQuery>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((GetInstrumentQuery query, CancellationToken _) =>
      {
        queried.Add($"{query.Ticker}|{query.ExchangeName}");

        return new InstrumentDetailDto { Ticker = query.Ticker, ExchangeName = query.ExchangeName, Currency = "USD" };
      });

    var form = new InstrumentDetailForm(_mediator.Object, _accounts.Object, Options.Create(new TelegramBotOptions()));

    var first = await form.RenderAsync(Context("AAPL · NASDAQ"), CancellationToken.None);
    var back = await form.RenderAsync(Context("Назад"), CancellationToken.None);

    Assert.Contains("AAPL (NASDAQ)", first.ReplyText);
    Assert.Equal(first.ReplyText, back.ReplyText);
    Assert.Equal(new[] { "AAPL|NASDAQ", "AAPL|NASDAQ" }, queried);
  }
}
