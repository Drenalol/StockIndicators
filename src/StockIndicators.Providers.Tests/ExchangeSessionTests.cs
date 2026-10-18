using System.Diagnostics;
using RateLimiter;
using StockIndicators.Application.Rules;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Base.Exchange;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class ExchangeSessionTests
{
  private static readonly DateOnly[] UsHolidays =
  [
    new(2026, 1, 1),
    new(2026, 1, 19),
    new(2026, 2, 16),
    new(2026, 4, 3),
    new(2026, 5, 25),
    new(2026, 6, 19),
    new(2026, 7, 3),
    new(2026, 9, 7),
    new(2026, 11, 26),
    new(2026, 12, 25)
  ];

  private static ExchangeSession CreateUsSession() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "America/New_York",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
    PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
    PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) },
    Holidays = UsHolidays
  });

  private static ExchangeSession CreateMoexSession() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "Europe/Moscow",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(10, 0, 0), Close = new TimeSpan(18, 45, 0) }],
    Holidays = [new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 23)]
  });

  private static ExchangeSession CreateCrossMidnightSession(DayOfWeek[] weekendDays, DateOnly[] holidays) => new(new ExchangeSessionOptions
  {
    TimeZoneId = "UTC",
    Sessions = [new ExchangeSessionWindow { Name = "Night", Open = new TimeSpan(23, 0, 0), Close = new TimeSpan(1, 0, 0) }],
    WeekendDays = weekendDays,
    Holidays = holidays
  });

  [Fact]
  public void Us_OpenAtExactOpen_Winter()
    => Assert.True(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_ClosedOneSecondBeforeOpen_Winter()
    => Assert.False(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 1, 15, 14, 29, 59, TimeSpan.Zero)));

  [Fact]
  public void Us_OpenAtExactOpen_Summer()
    => Assert.True(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 7, 15, 13, 30, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_ClosedOneSecondBeforeOpen_Summer()
    => Assert.False(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 7, 15, 13, 29, 59, TimeSpan.Zero)));

  [Fact]
  public void Us_OpenMidSession_Summer()
    => Assert.True(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 7, 15, 15, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_OpenOneSecondBeforeClose_Winter()
    => Assert.True(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 1, 15, 20, 59, 59, TimeSpan.Zero)));

  [Fact]
  public void Us_ClosedAtExactClose_Winter()
    => Assert.False(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 1, 15, 21, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_PreAndPostMarket_DetectedSeparately()
  {
    var session = CreateUsSession();

    Assert.True(session.IsPreMarketAt(new DateTimeOffset(2026, 1, 15, 13, 0, 0, TimeSpan.Zero)));
    Assert.False(session.IsOpenAt(new DateTimeOffset(2026, 1, 15, 13, 0, 0, TimeSpan.Zero)));
    Assert.True(session.IsPostMarketAt(new DateTimeOffset(2026, 1, 15, 22, 0, 0, TimeSpan.Zero)));
    Assert.False(session.IsPostMarketAt(new DateTimeOffset(2026, 1, 15, 20, 59, 59, TimeSpan.Zero)));
  }

  [Fact]
  public void Us_ClosedOnWeekend()
    => Assert.False(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 7, 18, 15, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_ClosedOnHoliday()
    => Assert.False(CreateUsSession().IsOpenAt(new DateTimeOffset(2026, 11, 26, 15, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextOpen_SameDayBeforeOpen()
    => Assert.Equal(
      new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero),
      CreateUsSession().GetNextOpen(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextOpen_FridayAfterClose_IsMonday()
    => Assert.Equal(
      new DateTimeOffset(2026, 7, 20, 13, 30, 0, TimeSpan.Zero),
      CreateUsSession().GetNextOpen(new DateTimeOffset(2026, 7, 17, 21, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextOpen_Saturday_IsMonday()
    => Assert.Equal(
      new DateTimeOffset(2026, 7, 20, 13, 30, 0, TimeSpan.Zero),
      CreateUsSession().GetNextOpen(new DateTimeOffset(2026, 7, 18, 15, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextOpen_Thanksgiving_IsNextRegularFriday()
    => Assert.Equal(
      new DateTimeOffset(2026, 11, 27, 14, 30, 0, TimeSpan.Zero),
      CreateUsSession().GetNextOpen(new DateTimeOffset(2026, 11, 26, 21, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextOpen_SaturdayBeforeHolidayMonday_SkipsToTuesday()
    => Assert.Equal(
      new DateTimeOffset(2026, 1, 20, 14, 30, 0, TimeSpan.Zero),
      CreateUsSession().GetNextOpen(new DateTimeOffset(2026, 1, 17, 22, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Moex_OpenAtExactOpen()
    => Assert.True(CreateMoexSession().IsOpenAt(new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Moex_ClosedAtExactClose()
    => Assert.False(CreateMoexSession().IsOpenAt(new DateTimeOffset(2026, 1, 12, 15, 45, 0, TimeSpan.Zero)));

  [Fact]
  public void Moex_ClosedOnHoliday()
    => Assert.False(CreateMoexSession().IsOpenAt(new DateTimeOffset(2026, 2, 23, 7, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Moex_NextOpen_Saturday_IsMonday()
    => Assert.Equal(
      new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero),
      CreateMoexSession().GetNextOpen(new DateTimeOffset(2026, 1, 10, 7, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Moex_NextOpen_HolidayMonday_IsTuesday()
    => Assert.Equal(
      new DateTimeOffset(2026, 2, 24, 7, 0, 0, TimeSpan.Zero),
      CreateMoexSession().GetNextOpen(new DateTimeOffset(2026, 2, 23, 7, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void CrossMidnight_OpenOnBothSidesOfMidnight()
  {
    var session = CreateCrossMidnightSession([DayOfWeek.Saturday, DayOfWeek.Sunday], []);

    Assert.True(session.IsOpenAt(new DateTimeOffset(2026, 1, 14, 23, 30, 0, TimeSpan.Zero)));
    Assert.True(session.IsOpenAt(new DateTimeOffset(2026, 1, 15, 0, 30, 0, TimeSpan.Zero)));
    Assert.False(session.IsOpenAt(new DateTimeOffset(2026, 1, 15, 1, 30, 0, TimeSpan.Zero)));
  }

  [Fact]
  public void CrossMidnight_WindowBelongsToPreviousDay_SkipsWeekend()
  {
    var session = CreateCrossMidnightSession([DayOfWeek.Saturday, DayOfWeek.Sunday], []);

    Assert.True(session.IsOpenAt(new DateTimeOffset(2026, 1, 10, 0, 30, 0, TimeSpan.Zero)));
    Assert.False(session.IsOpenAt(new DateTimeOffset(2026, 1, 10, 23, 30, 0, TimeSpan.Zero)));
    Assert.False(session.IsOpenAt(new DateTimeOffset(2026, 1, 11, 0, 30, 0, TimeSpan.Zero)));
  }

  [Fact]
  public void CrossMidnight_PreviousDayHoliday_ClosesWindow()
  {
    var session = CreateCrossMidnightSession([DayOfWeek.Saturday, DayOfWeek.Sunday], [new DateOnly(2026, 1, 9)]);

    Assert.False(session.IsOpenAt(new DateTimeOffset(2026, 1, 10, 0, 30, 0, TimeSpan.Zero)));
  }

  [Fact]
  public void CrossMidnight_NextOpen_SkipsWeekend()
  {
    var session = CreateCrossMidnightSession([DayOfWeek.Saturday, DayOfWeek.Sunday], []);

    Assert.Equal(
      new DateTimeOffset(2026, 1, 12, 23, 0, 0, TimeSpan.Zero),
      session.GetNextOpen(new DateTimeOffset(2026, 1, 9, 23, 30, 0, TimeSpan.Zero)));
  }

  [Fact]
  public async Task Constraint_WhenOpen_ReturnsImmediately()
  {
    var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 14, 15, 0, 0, TimeSpan.Zero));
    var session = CreateAlwaysOpenSession(clock);

    var stopwatch = Stopwatch.StartNew();
    await new ExchangeSessionAwaitableConstraint(session).WaitForReadiness(CancellationToken.None);

    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
  }

  [Fact]
  public async Task Constraint_WhenClosed_WaitsUntilNextOpen()
  {
    var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 14, 23, 59, 59, 500, TimeSpan.Zero));
    var session = CreateAlwaysOpenSession(clock);

    var stopwatch = Stopwatch.StartNew();
    await new ExchangeSessionAwaitableConstraint(session, TimeSpan.FromMilliseconds(100)).WaitForReadiness(CancellationToken.None);
    stopwatch.Stop();

    Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(400), $"expected ~0.5s wait, got {stopwatch.Elapsed}");
  }

  [Fact]
  public async Task Constraint_WhenClosed_CancellationStopsTheWait()
  {
    var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 14, 23, 30, 0, TimeSpan.Zero));
    var session = new ExchangeSession(new ExchangeSessionOptions
    {
      TimeZoneId = "UTC",
      Sessions = [new ExchangeSessionWindow { Open = TimeSpan.Zero, Close = TimeSpan.FromSeconds(1) }]
    }, clock);

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    var stopwatch = Stopwatch.StartNew();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
      () => new ExchangeSessionAwaitableConstraint(session, TimeSpan.Zero).WaitForReadiness(cts.Token));

    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
  }

  [Fact]
  public async Task NoOpConstraint_ReturnsImmediately()
  {
    var stopwatch = Stopwatch.StartNew();
    await NoOpAwaitableConstraint.Instance.WaitForReadiness(CancellationToken.None);

    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task FromMaxCountByIntervalTimeLimiter_StillRateLimits()
  {
    var limiter = new FromMaxCountByIntervalTimeLimiter(1, TimeSpan.FromMilliseconds(200));
    await limiter.WaitAsync(CancellationToken.None);

    var stopwatch = Stopwatch.StartNew();
    await limiter.WaitAsync(CancellationToken.None);

    Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150), $"expected ~200ms wait, got {stopwatch.Elapsed}");
  }

  [Fact]
  public async Task FromMaxCountByIntervalTimeLimiter_PropagatesCancellation()
  {
    var limiter = new FromMaxCountByIntervalTimeLimiter(1, TimeSpan.FromSeconds(10));
    await limiter.WaitAsync(CancellationToken.None);

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    var stopwatch = Stopwatch.StartNew();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.WaitAsync(cts.Token));
    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
  }

  [Fact]
  public async Task ComposedTimeLimiter_PropagatesCancellation()
  {
    var limiter = new ComposedTimeLimiter(new CountByIntervalAwaitableConstraint(1, TimeSpan.FromSeconds(10)));
    await limiter.WaitAsync(CancellationToken.None);

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    var stopwatch = Stopwatch.StartNew();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.WaitAsync(cts.Token));
    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
  }

  [Fact]
  public async Task ComposedTimeLimiter_WaitsExchangeFirstThenRateLimit()
  {
    var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 14, 23, 59, 58, TimeSpan.Zero));
    var session = new ExchangeSession(new ExchangeSessionOptions
    {
      TimeZoneId = "UTC",
      Sessions = [new ExchangeSessionWindow { Open = TimeSpan.Zero, Close = TimeSpan.FromSeconds(30) }]
    }, clock);

    var rateLimit = new CountByIntervalAwaitableConstraint(1, TimeSpan.FromSeconds(2.5));
    var consumed = new ComposedTimeLimiter(NoOpAwaitableConstraint.Instance, rateLimit);
    await consumed.WaitAsync(CancellationToken.None);

    var limiter = new ComposedTimeLimiter(new ExchangeSessionAwaitableConstraint(session, TimeSpan.Zero), rateLimit);
    var stopwatch = Stopwatch.StartNew();
    await limiter.WaitAsync(CancellationToken.None);
    stopwatch.Stop();

    Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(2.4), $"expected ~2.5s wait, got {stopwatch.Elapsed}");
  }

  private static ExchangeSession CreateAlwaysOpenSession(TimeProvider clock) => new(new ExchangeSessionOptions
  {
    TimeZoneId = "UTC",
    Sessions = [new ExchangeSessionWindow { Open = TimeSpan.Zero, Close = new TimeSpan(23, 59, 59) }]
  }, clock);

  // --- Extended sessions: the poller runs in them and the charts price candles from them ---

  [Theory]
  [InlineData(2026, 1, 15, 7, 0, null)] // 02:00 ET: deep night, no window at all
  [InlineData(2026, 1, 15, 12, 0, ExtendedSessionKind.Pre)] // 07:00 ET pre-market
  [InlineData(2026, 1, 15, 15, 0, null)] // 10:00 ET regular
  [InlineData(2026, 1, 15, 21, 0, ExtendedSessionKind.Post)] // 16:00 ET post-market start
  [InlineData(2026, 1, 15, 23, 30, ExtendedSessionKind.Post)] // 18:30 ET still post-market
  [InlineData(2026, 1, 16, 1, 0, null)] // 20:00 ET: the post window closes exclusively
  public void Us_ExtendedKindAt(int year, int month, int day, int hourUtc, int minuteUtc, ExtendedSessionKind? expected)
    => Assert.Equal(expected, CreateUsSession().ExtendedKindAt(new DateTimeOffset(year, month, day, hourUtc, minuteUtc, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextExtendedOpen_FromPostMarketIsTheNextPreMarket()
    // Fri 2026-01-16 01:30 UTC = Thu 20:30 ET: the post window ended, the next window of
    // the next trading day is Friday's pre-market at 04:00 EST (09:00 UTC)
    => Assert.Equal(
      new DateTimeOffset(2026, 1, 16, 9, 0, 0, TimeSpan.Zero),
      CreateUsSession().GetNextExtendedOpen(new DateTimeOffset(2026, 1, 16, 1, 30, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextExtendedOpen_SkipsWeekendAndHoliday()
    // Sat 2026-01-17: Monday the 19th is the MLK holiday in the test calendar, so the next
    // window is Tuesday's pre-market
    => Assert.Equal(
      new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero),
      CreateUsSession().GetNextExtendedOpen(new DateTimeOffset(2026, 1, 17, 12, 0, 0, TimeSpan.Zero)));

  [Fact]
  public void Us_NextExtendedOpen_BeforePreMarketIsSameDayPreMarket()
    => Assert.Equal(
      new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero),
      CreateUsSession().GetNextExtendedOpen(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero)));

  [Fact]
  public async Task Constraint_ExtendedMode_ReadyInsideExtendedWindow()
  {
    // Thu 2026-01-15 12:00 UTC = 07:00 ET, pre-market: the regular gate would idle until
    // 09:30, the extended gate must return immediately
    var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
    var session = new ExchangeSession(new ExchangeSessionOptions
    {
      TimeZoneId = "America/New_York",
      Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
      PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
      PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) }
    }, clock);

    var stopwatch = Stopwatch.StartNew();
    await new ExchangeSessionAwaitableConstraint(session, includeExtendedSessions: true).WaitForReadiness(CancellationToken.None);

    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
  }

  [Fact]
  public async Task Constraint_RegularMode_IdlesThroughExtendedHours()
  {
    var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
    var session = new ExchangeSession(new ExchangeSessionOptions
    {
      TimeZoneId = "America/New_York",
      Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
      PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
      PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) }
    }, clock);

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    var stopwatch = Stopwatch.StartNew();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
      () => new ExchangeSessionAwaitableConstraint(session).WaitForReadiness(cts.Token));

    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
  }

  private sealed class FixedTimeProvider : TimeProvider
  {
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
  }

  // --- Overnight window: session day model and poller opens ---

  private static ExchangeSession CreateUsSessionOvernight() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "America/New_York",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
    PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
    PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) },
    Overnight = new ExchangeSessionWindow { Name = "Overnight", Open = new TimeSpan(20, 0, 0), Close = new TimeSpan(4, 0, 0) },
    Holidays = UsHolidays
  });

  // Jan 19 2026 is the MLK holiday in the test calendar: the Sunday-evening session
  // therefore prices Tuesday the 20th. expectedDay is the day-of-January of the session
  // day (-1 when the moment is not in the overnight window).
  [Theory]
  [InlineData(2026, 1, 14, 23, -1)] // Wed 18:00 ET: post-market, not overnight
  [InlineData(2026, 1, 15, 3, 15)] // Wed 22:00 ET: overnight prices Thursday
  [InlineData(2026, 1, 15, 7, 15)] // Thu 02:00 ET: tail of the Wednesday-evening session
  [InlineData(2026, 1, 15, 9, -1)] // Thu 04:00 ET: pre-market starts, the overnight window ended
  [InlineData(2026, 1, 16, 3, 16)] // Thu 22:00 ET: prices Friday
  [InlineData(2026, 1, 17, 3, -1)] // Fri 22:00 ET: Friday night is dark
  [InlineData(2026, 1, 19, 3, 20)] // Sun 22:00 ET: prices Tuesday (Monday is the MLK holiday)
  public void Us_OvernightSessionDay(int y, int mo, int d, int hourUtc, int expectedDay)
  {
    var utc = new DateTimeOffset(y, mo, d, hourUtc, 0, 0, TimeSpan.Zero);

    var day = CreateUsSessionOvernight().OvernightSessionDayAt(utc);

    if (expectedDay < 0)
    {
      Assert.Null(day);
      return;
    }

    Assert.Equal(new DateOnly(2026, 1, expectedDay), day);
  }

  [Fact]
  public void Us_NextExtendedOpen_FromFridayNightIsSundayOvernight()
  {
    // Fri 21:00 ET (Jan 16): the post window has closed and Friday night is dark; the next
    // open of ANY window is the Sunday-evening overnight session (Jan 18 20:00 ET)
    var next = CreateUsSessionOvernight().GetNextExtendedOpen(new DateTimeOffset(2026, 1, 17, 2, 0, 0, TimeSpan.Zero));

    Assert.Equal(new DateTimeOffset(2026, 1, 19, 1, 0, 0, TimeSpan.Zero), next);
  }

  [Fact]
  public void Us_NextExtendedOpen_FromWeekendGapWithoutOvernight_SkipsToPreMarket()
    => Assert.Equal(
      // Jan 19 is the MLK holiday in the test calendar, so without an overnight window the
      // next open of any session is Tuesday's pre-market
      new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero),
      CreateUsSession().GetNextExtendedOpen(new DateTimeOffset(2026, 1, 17, 2, 0, 0, TimeSpan.Zero)));
}