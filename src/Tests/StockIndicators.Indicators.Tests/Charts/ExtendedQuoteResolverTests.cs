using Moq;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class ExtendedQuoteResolverTests
{
  private static readonly UserLocale Locale = UserLocale.Default;

  private static Candle MinuteAt(DateTime utc, decimal close) =>
    new("AAPL", utc, TimeFrame.Minute, 1, 2, 0.5m, close, 100m, true, true, "NASDAQ");

  private static Candle Daily(DateTime utc, decimal close) =>
    new("AAPL", utc, TimeFrame.Day, close, close, close, close, 1000m, true, true, "NASDAQ");

  private readonly DateTime _postMarketMinute = new(2026, 1, 15, 21, 0, 0, DateTimeKind.Utc);

  [Fact]
  public async Task ExtendedMinute_BecomesAQuoteAgainstTheDailyReference()
  {
    var repository = new Mock<ICandleRepository>();
    repository
      .Setup(r => r.GetLastCandlesAsync("AAPL", "NASDAQ", It.IsAny<int>(), TimeFrame.Minute, It.IsAny<CancellationToken>()))
      .ReturnsAsync([MinuteAt(_postMarketMinute, 336.5m)]);

    var gate = new Mock<ITradingSessionGate>();
    gate
      .Setup(g => g.ResolveExtendedQuote("NASDAQ", _postMarketMinute, It.IsAny<IReadOnlyList<Candle>>()))
      .Returns(new ExtendedQuoteInfo(ExtendedSessionKind.Post, 336.67));

    var dailies = new[] { Daily(new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc), 336.67m) };

    var resolution = await ExtendedQuoteResolver.ResolveAsync(repository.Object, gate.Object, "AAPL", "NASDAQ", dailies, Locale);

    var quote = resolution.Quote;
    Assert.NotNull(quote);
    Assert.Equal("post", quote!.Session);
    Assert.Equal(336.5, quote.Price);
    Assert.Equal(336.67, quote.ReferenceClose);
    Assert.Equal(_postMarketMinute, quote.At);
    Assert.Equal("After-hours", quote.Label);
  }

  [Fact]
  public async Task OvernightMinute_ReportsOvernightSessionWithLocalizedLabel()
  {
    var overnightMinute = new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc);

    var repository = new Mock<ICandleRepository>();
    repository
      .Setup(r => r.GetLastCandlesAsync("AAPL", "NASDAQ", It.IsAny<int>(), TimeFrame.Minute, It.IsAny<CancellationToken>()))
      .ReturnsAsync([MinuteAt(overnightMinute, 336.4m)]);

    var gate = new Mock<ITradingSessionGate>();
    gate
      .Setup(g => g.ResolveExtendedQuote("NASDAQ", overnightMinute, It.IsAny<IReadOnlyList<Candle>>()))
      .Returns(new ExtendedQuoteInfo(ExtendedSessionKind.Overnight, 336.67));

    var resolution = await ExtendedQuoteResolver.ResolveAsync(
      repository.Object, gate.Object, "AAPL", "NASDAQ", Array.Empty<Candle>(), new UserLocale(UserLocale.Russian, TimeZoneInfo.Utc));

    Assert.Equal("overnight", resolution.Quote!.Session);
    Assert.Equal("Ночная сессия", resolution.Quote.Label);
  }

  [Fact]
  public async Task RegularMinute_ResolvesNothing()
  {
    var regular = new DateTime(2026, 1, 15, 15, 0, 0, DateTimeKind.Utc);

    var repository = new Mock<ICandleRepository>();
    repository
      .Setup(r => r.GetLastCandlesAsync("AAPL", "NASDAQ", It.IsAny<int>(), TimeFrame.Minute, It.IsAny<CancellationToken>()))
      .ReturnsAsync([MinuteAt(regular, 336m)]);

    var gate = new Mock<ITradingSessionGate>();
    gate
      .Setup(g => g.ResolveExtendedQuote("NASDAQ", regular, It.IsAny<IReadOnlyList<Candle>>()))
      .Returns((ExtendedQuoteInfo?)null);

    var resolution = await ExtendedQuoteResolver.ResolveAsync(repository.Object, gate.Object, "AAPL", "NASDAQ", Array.Empty<Candle>(), Locale);

    Assert.Null(resolution.Quote);
    Assert.Empty(resolution.Candles);
  }

  [Fact]
  public async Task NoMinuteCandle_ResolvesNothing()
  {
    var repository = new Mock<ICandleRepository>();
    repository
      .Setup(r => r.GetLastCandlesAsync("AAPL", "NASDAQ", It.IsAny<int>(), TimeFrame.Minute, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<Candle>());

    var gate = new Mock<ITradingSessionGate>();

    var dailies = new[] { Daily(new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc), 1m) };
    var resolution = await ExtendedQuoteResolver.ResolveAsync(repository.Object, gate.Object, "AAPL", "NASDAQ", dailies, Locale);

    Assert.Null(resolution.Quote);
    Assert.Same(dailies, resolution.Candles);

    gate.Verify(g => g.ResolveExtendedQuote(It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyList<Candle>>()), Times.Never);
  }

  [Fact]
  public async Task MissingGate_ResolvesNothing()
  {
    var repository = new Mock<ICandleRepository>();
    var dailies = new[] { Daily(new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc), 1m) };

    var resolution = await ExtendedQuoteResolver.ResolveAsync(repository.Object, null, "AAPL", "NASDAQ", dailies, Locale);

    Assert.Null(resolution.Quote);
    Assert.Same(dailies, resolution.Candles);

    // no gate means the extended feature is inert: not even the minute lookup runs
    repository.Verify(r => r.GetLastCandlesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<TimeFrame>(), It.IsAny<CancellationToken>()), Times.Never);
  }
}
