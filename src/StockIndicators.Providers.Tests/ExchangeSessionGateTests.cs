using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Providers.Base.Exchange;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class ExchangeSessionGateTests
{
  private static ExchangeSession CreateUsSession() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "America/New_York",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
    PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
    PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) },
    Holidays = [new DateOnly(2026, 1, 1)]
  });

  // MOEX configures only the regular window: it has no extended sessions to gate.
  private static ExchangeSession CreateMoexSession() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "Europe/Moscow",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(10, 0, 0), Close = new TimeSpan(18, 45, 0) }]
  });

  private static ExchangeSessionGate CreateGate() => new(CreateUsSession(), CreateMoexSession());

  // 2026-01-15 is a Thursday: 07:00 UTC is 02:00 ET (nothing), 12:00 UTC is 07:00 ET
  // (pre-market), 15:00 UTC is 10:00 ET (regular), 21:00 UTC is 16:00 ET (post-market).
  [Theory]
  [InlineData(7, 0, false)] // before pre-market: no candles anyway
  [InlineData(12, 0, true)] // pre-market
  [InlineData(15, 0, false)] // regular
  [InlineData(20, 30, false)] // regular, right up to the close
  [InlineData(21, 0, true)] // post-market starts
  [InlineData(23, 0, true)] // still post-market (18:00 ET, the window runs to 20:00)
  public void UsMinuteCandle_DependentOnSession(int hourUtc, int minuteUtc, bool expected)
    => Assert.Equal(expected, CreateGate().IsExtendedSession("NASDAQ", TimeFrame.Minute, new DateTime(2026, 1, 15, hourUtc, minuteUtc, 0, DateTimeKind.Utc)));

  // CurrentMarketSession is the "is the exchange open" answer for the chart status:
  // pre/post/regular phases, the dark gaps as closed, nothing at all for venues the
  // gate does not model (24/7 crypto).
  [Theory]
  [InlineData(7, MarketSession.Closed)] // 02:00 ET: the dark gap before pre-market
  [InlineData(12, MarketSession.PreMarket)] // 07:00 ET
  [InlineData(15, MarketSession.Regular)] // 10:00 ET
  [InlineData(21, MarketSession.AfterHours)] // 16:00 ET
  public void CurrentMarketSession_UsExchange_PhasesTheDay(int hourUtc, MarketSession expected)
    => Assert.Equal(expected, CreateGate().CurrentMarketSession("NASDAQ", new DateTime(2026, 1, 15, hourUtc, 0, 0, DateTimeKind.Utc)));

  [Fact]
  public void CurrentMarketSession_ExchangeHoliday_IsClosed()
    => Assert.Equal(MarketSession.Closed, CreateGate().CurrentMarketSession("NYSE", new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc)));

  [Fact]
  public void CurrentMarketSession_MoexRegularSession_IsRegular()
    => Assert.Equal(MarketSession.Regular, CreateGate().CurrentMarketSession("MOEX", new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc))); // 11:00 MSK

  [Fact]
  public void CurrentMarketSession_UnknownCryptoVenue_HasNoPhase()
    => Assert.Null(CreateGate().CurrentMarketSession("BINANCE", new DateTime(2026, 1, 15, 21, 0, 0, DateTimeKind.Utc)));

  [Fact]
  public void NonMinuteTimeframes_AreNeverGated()
  {
    var gate = CreateGate();
    var postMarketUtc = new DateTime(2026, 1, 15, 21, 0, 0, DateTimeKind.Utc);

    // Higher timeframes fold the extended minutes into one bar already; gating their
    // timestamps (e.g. a daily candle at 00:00) would silence the whole rule.
    Assert.False(gate.IsExtendedSession("NASDAQ", TimeFrame.Hour, postMarketUtc));
    Assert.False(gate.IsExtendedSession("NASDAQ", TimeFrame.Day, postMarketUtc));
  }

  // SessionDayOfMinute attributes every session minute (regular, pre, post) to its
  // exchange-local trading day, so the chart can compose the day's live bar from minutes.
  [Theory]
  [InlineData(7, 0, false)] // 02:00 ET: outside every session
  [InlineData(12, 0, true)] // pre-market minute
  [InlineData(15, 0, true)] // regular-session minute
  [InlineData(20, 30, true)] // regular session, up to the close
  [InlineData(21, 0, true)] // post-market minute
  [InlineData(23, 0, true)] // still post-market (18:00 ET)
  public void UsMinute_AttributedToItsTradingDay(int hourUtc, int minuteUtc, bool attributedToThursday)
  {
    var day = CreateGate().SessionDayOfMinute("NASDAQ", new DateTime(2026, 1, 15, hourUtc, minuteUtc, 0, DateTimeKind.Utc));

    Assert.Equal(attributedToThursday ? new DateOnly(2026, 1, 15) : (DateOnly?) null, day);
  }

  [Fact]
  public void UsMinute_AttributedToPreviousLocalDay_AfterMidnightUtc()
  {
    // Fri 00:30 UTC = Thu 19:30 ET (post-market): the minute prices Thursday
    var day = CreateGate().SessionDayOfMinute("NASDAQ", new DateTime(2026, 1, 16, 0, 30, 0, DateTimeKind.Utc));

    Assert.Equal(new DateOnly(2026, 1, 15), day);
  }

  [Fact]
  public void UsMinute_OnExchangeHoliday_AttributesToNoDay()
  {
    // New Year's Day is a holiday in the test session config
    var day = CreateGate().SessionDayOfMinute("NASDAQ", new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc));

    Assert.Null(day);
  }

  [Theory]
  [InlineData(2026, 1, 15, 8, true)] // 11:00 MSK: regular session → Jan 15
  [InlineData(2026, 1, 15, 5, false)] // 08:00 MSK: before the session
  [InlineData(2026, 1, 15, 22, false)] // 01:00 MSK next day: closed
  public void MoexMinute_AttributedToItsTradingDay(int year, int month, int dayNum, int hourUtc, bool attributed)
  {
    var day = CreateGate().SessionDayOfMinute("MOEX", new DateTime(year, month, dayNum, hourUtc, 0, 0, DateTimeKind.Utc));

    Assert.Equal(attributed ? new DateOnly(2026, 1, 15) : (DateOnly?) null, day);
  }

  [Theory]
  [InlineData("NASDAQ")]
  [InlineData("NYSE")]
  [InlineData("nyse")] // exchange names are matched case-insensitively
  [InlineData("IEX")]
  [InlineData("NYSE MKT")]
  public void AllTrackedUsExchanges_UseTheUsSession(string exchangeName)
    => Assert.True(CreateGate().IsExtendedSession(exchangeName, TimeFrame.Minute, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)));

  [Theory]
  [InlineData("BINANCE")] // 24/7: no session configured
  [InlineData("MOEX")] // no pre/post windows configured
  [InlineData("SOMETHING_ELSE")]
  [InlineData(null)]
  public void ExchangesWithoutExtendedSessions_AreNeverGated(string? exchangeName)
    => Assert.False(CreateGate().IsExtendedSession(exchangeName, TimeFrame.Minute, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)));

  // With the providers disabled the gate resolves no sessions at all and must stay inert.
  [Fact]
  public void NullSessions_NeverGate()
    => Assert.False(new ExchangeSessionGate(null, null).IsExtendedSession("NASDAQ", TimeFrame.Minute, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)));

  // --- Extended quote resolution (the "Pre-market/After-hours" line of the charts) ---

  // Same day anchors: 2026-01-15 Thu. Pre = 07:00 ET (12:00 UTC), post = 16:00-20:00 ET
  // (21:00-2026-01-16T01:00 UTC). Daily bars are keyed at the exchange-local midnight.
  private static Candle Daily(DateOnly day, double close) =>
    new("AAPL", day.ToDateTime(new TimeOnly(5, 0, 0), DateTimeKind.Utc), TimeFrame.Day, (decimal)close, (decimal)close, (decimal)close, (decimal)close, 1000m);

  private static Candle MinuteAt(int hourUtc, int minuteUtc) =>
    new("AAPL", new DateTime(2026, 1, 15, hourUtc, minuteUtc, 0, DateTimeKind.Utc), TimeFrame.Minute, 1m, 2m, 0.5m, 336.5m, 100m);

  [Fact]
  public void PostMarketMinute_PricesAgainstItsOwnDayDailyBar()
  {
    var dailies = new[] { Daily(new DateOnly(2026, 1, 14), 100), Daily(new DateOnly(2026, 1, 15), 110) };

    var info = CreateGate().ResolveExtendedQuote("NASDAQ", MinuteAt(21, 0).Timestamp, dailies);

    Assert.NotNull(info);
    Assert.Equal(ExtendedSessionKind.Post, info!.Value.Kind);
    Assert.Equal(110, info.Value.ReferenceClose);
  }

  [Fact]
  public void PreMarketMinute_PricesAgainstTheLastClosedDailyBar()
  {
    var dailies = new[] { Daily(new DateOnly(2026, 1, 13), 100), Daily(new DateOnly(2026, 1, 14), 105) };

    var info = CreateGate().ResolveExtendedQuote("NASDAQ", MinuteAt(12, 0).Timestamp, dailies);

    Assert.NotNull(info);
    Assert.Equal(ExtendedSessionKind.Pre, info!.Value.Kind);
    Assert.Equal(105, info.Value.ReferenceClose);
  }

  [Fact]
  public void ExtendedMinute_WithoutItsDailyBar_ReportsNoReference()
  {
    var info = CreateGate().ResolveExtendedQuote("NASDAQ", MinuteAt(21, 0).Timestamp, new[] { Daily(new DateOnly(2026, 1, 14), 100) });

    Assert.NotNull(info);
    Assert.Equal(ExtendedSessionKind.Post, info!.Value.Kind);
    Assert.Null(info.Value.ReferenceClose);
  }

  [Fact]
  public void RegularMinute_IsNotAnExtendedQuote()
    => Assert.Null(CreateGate().ResolveExtendedQuote("NASDAQ", MinuteAt(15, 0).Timestamp, Array.Empty<Candle>()));

  [Theory]
  [InlineData("MOEX")] // no extended windows configured
  [InlineData("BINANCE")] // 24/7 exchange, no session at all
  [InlineData("SOMETHING_ELSE")]
  [InlineData(null)]
  public void ExchangesWithoutExtendedWindows_HaveNoExtendedQuote(string? exchangeName)
    => Assert.Null(CreateGate().ResolveExtendedQuote(exchangeName, MinuteAt(21, 0).Timestamp, Array.Empty<Candle>()));

  [Fact]
  public void NullSessions_HaveNoExtendedQuote()
    => Assert.Null(new ExchangeSessionGate(null, null).ResolveExtendedQuote("NASDAQ", MinuteAt(21, 0).Timestamp, Array.Empty<Candle>()));

  // --- Overnight session (BOATS-style 20:00-04:00 evenings, prices the next trading day) ---

  private static ExchangeSession CreateUsSessionWithOvernight() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "America/New_York",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
    PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
    PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) },
    Overnight = new ExchangeSessionWindow { Name = "Overnight", Open = new TimeSpan(20, 0, 0), Close = new TimeSpan(4, 0, 0) },
    Holidays = [new DateOnly(2026, 1, 1)]
  });

  private static ExchangeSessionGate CreateOvernightGate() => new(CreateUsSessionWithOvernight(), CreateMoexSession());

  // The overnight window prices the NEXT trading day: the Wednesday-evening minute (Wed
  // 22:00 ET = Thu 03:00 UTC) and its Thursday-early-morning tail (Thu 02:00 ET = 07:00 UTC)
  // both belong to Thursday Jan 15; Friday and Saturday nights are dark. expectedDay is the
  // day-of-January of the session day (-1 when the moment is not an extended minute).
  [Theory]
  [InlineData(2026, 1, 14, 23, 14)] // Wed 18:00 ET: post-market minutes stay on Wednesday
  [InlineData(2026, 1, 15, 3, 15)] // Wed 22:00 ET: prices Thursday
  [InlineData(2026, 1, 15, 7, 15)] // Thu 02:00 ET tail: still Thursday
  [InlineData(2026, 1, 15, 12, 15)] // Thu 07:00 ET: pre-market stays on Thursday
  [InlineData(2026, 1, 16, 3, 16)] // Thu 22:00 ET: prices Friday
  [InlineData(2026, 1, 17, 3, -1)] // Fri 22:00 ET: Friday night is dark
  [InlineData(2026, 1, 18, 3, -1)] // Sat 22:00 ET: Saturday night is dark
  public void OvernightMinute_AttributesToTheSessionDay(int y, int mo, int d, int hourUtc, int expectedDay)
  {
    var minuteUtc = new DateTime(y, mo, d, hourUtc, 0, 0, DateTimeKind.Utc);

    var day = CreateOvernightGate().SessionDayOfExtendedMinute("NASDAQ", minuteUtc);

    if (expectedDay < 0)
    {
      Assert.Null(day);
      return;
    }

    Assert.Equal(new DateOnly(y, mo, expectedDay), day);
  }

  [Fact]
  public void OvernightSession_ResolvesQuoteAgainstTheLastClosedDay()
  {
    var gate = CreateOvernightGate();

    // Thu 03:00 UTC = Wed 22:00 ET; the session prices Thursday, so the reference is the
    // last closed daily bar (Wednesday's), not Thursday's (which does not exist yet)
    var dailies = new[]
    {
      Daily(new DateOnly(2026, 1, 13), 100),
      Daily(new DateOnly(2026, 1, 14), 105),
    };

    var info = gate.ResolveExtendedQuote("NASDAQ", new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc), dailies);

    Assert.Equal(ExtendedSessionKind.Overnight, info!.Value.Kind);
    Assert.Equal(105, info.Value.ReferenceClose);
  }

  [Fact]
  public void OvernightSession_CurrentExtendedDayIsTheFormingTradingDay()
  {
    var gate = CreateOvernightGate();

    // Wed 22:00 ET forms Thursday's bar
    Assert.Equal(
      new DateOnly(2026, 1, 15),
      gate.CurrentExtendedDay("NASDAQ", new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc)));

    // Friday night is dark: no day forming
    Assert.Null(gate.CurrentExtendedDay("NASDAQ", new DateTime(2026, 1, 17, 3, 0, 0, DateTimeKind.Utc)));
  }

  [Fact]
  public void Overnight_IsExtendedSessionForMinuteGating()
  {
    var gate = CreateOvernightGate();

    // Wed 22:00 ET overnight minute must be gated like pre/post: it must not fold into the
    // daily bars nor wake rule conditions
    Assert.True(gate.IsExtendedSession("NASDAQ", TimeFrame.Minute, new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc)));
    // and a gate WITHOUT the overnight window (the current MOEX/default setup) treats the
    // same evening minute as not extended (no session configured)
    Assert.False(CreateGate().IsExtendedSession("NASDAQ", TimeFrame.Minute, new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc)));
  }
}