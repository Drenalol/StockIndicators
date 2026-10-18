using Moq;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;
using Xunit;

namespace StockIndicators.Indicators.Tests.Charts;

public class ExtendedChartComposerTests
{
  // 2026-01-15 is a Thursday. The exchange midnight anchor of the day is 05:00Z (EST);
  // the UTC+5 offset here is only a convention stand-in for "the exchange's own midnight".
  private static readonly DateTime DayAnchor = new(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime NowInPreMarket = new(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc);
  private static readonly DateTime NowInRegular = new(2026, 1, 15, 15, 30, 0, DateTimeKind.Utc);
  private static readonly DateTime NowInPostMarket = new(2026, 1, 15, 21, 30, 0, DateTimeKind.Utc);

  private static DateOnly Day => DateOnly.FromDateTime(DayAnchor);

  private static Candle Daily(decimal open, decimal high, decimal low, decimal close, decimal volume, DateTime? timestamp = null) =>
    new("AAPL", timestamp ?? DayAnchor, TimeFrame.Day, open, high, low, close, volume, true, false, "NASDAQ");

  private static Candle Minute(DateTime utc, decimal open, decimal high, decimal low, decimal close, decimal volume) =>
    new("AAPL", utc, TimeFrame.Minute, open, high, low, close, volume, true, true, "NASDAQ");

  /// <summary>
  /// Gate stand-in: the day being formed, the anchor of a daily bar, and the (minute → day)
  /// attributions for all sessions / for extended sessions only.
  /// </summary>
  private static Mock<ITradingSessionGate> CreateGate(
    DateOnly? currentDay,
    DateTime anchorUtc,
    Dictionary<DateTime, DateOnly>? minuteDays = null,
    Dictionary<DateTime, DateOnly>? extendedMinuteDays = null)
  {
    var gate = new Mock<ITradingSessionGate>();

    gate.Setup(g => g.CurrentExtendedDay("NASDAQ", It.IsAny<DateTime>())).Returns(currentDay);
    gate.Setup(g => g.DailyAnchorUtc("NASDAQ", It.IsAny<DateOnly>())).Returns(anchorUtc);
    gate.Setup(g => g.SessionDayOfMinute(It.IsAny<string?>(), It.IsAny<DateTime>()))
      .Returns((string? _, DateTime t) => minuteDays is not null && minuteDays.TryGetValue(t, out var d) ? d : (DateOnly?) null);
    gate.Setup(g => g.SessionDayOfExtendedMinute(It.IsAny<string?>(), It.IsAny<DateTime>()))
      .Returns((string? _, DateTime t) => extendedMinuteDays is not null && extendedMinuteDays.TryGetValue(t, out var d) ? d : (DateOnly?) null);

    return gate;
  }

  private static Dictionary<DateTime, DateOnly> AllDay(params DateTime[] minutes) =>
    minutes.ToDictionary(t => t, _ => Day);

  [Fact]
  public void NoGate_KeepsTheSeriesUntouched()
  {
    var dailies = new[] { Daily(1, 2, 0.5m, 1.5m, 100) };

    Assert.Same(dailies, ExtendedChartComposer.Merge(dailies, Array.Empty<Candle>(), null, NowInPreMarket));
  }

  [Fact]
  public void WeekendGap_KeepsTheSeriesUntouched()
  {
    var dailies = new[] { Daily(1, 2, 0.5m, 1.5m, 100) };
    var gate = CreateGate(null, DayAnchor);
    var minutes = new[] { Minute(DayAnchor.AddHours(-30), 1, 1, 1, 1, 1) };

    Assert.Same(dailies, ExtendedChartComposer.Merge(dailies, minutes, gate.Object, new DateTime(2026, 1, 17, 15, 0, 0, DateTimeKind.Utc)));
  }

  [Fact]
  public void PreMarketBeforeTheFirstTrade_AppendsTheSyntheticLiveBar()
  {
    var yesterday = Daily(100, 102, 99, 101, 1_000_000, DayAnchor.AddDays(-1));
    var dailies = new[] { yesterday };

    var pre1 = DayAnchor.AddHours(7);
    var pre2 = DayAnchor.AddHours(8);
    var gate = CreateGate(Day, DayAnchor, AllDay(pre1, pre2), AllDay(pre1, pre2));

    var minutes = new[]
    {
      Minute(pre1, 331m, 332m, 330.5m, 331.5m, 500),
      Minute(pre2, 331.5m, 331.8m, 331m, 331.2m, 300),
    };

    var merged = ExtendedChartComposer.Merge(dailies, minutes, gate.Object, NowInPreMarket);

    Assert.Equal(2, merged.Length);
    var live = merged[^1];
    Assert.Equal(DayAnchor, DateTime.SpecifyKind(live.Timestamp, DateTimeKind.Utc));
    Assert.Equal(331m, live.Open);
    Assert.Equal(332m, live.High);
    Assert.Equal(330.5m, live.Low);
    Assert.Equal(331.2m, live.Close); // the newest pre-market minute is the live price
    Assert.Equal(800m, live.Volume); // no regular bar to add
    Assert.False(live.IsCompleted);
  }

  [Fact]
  public void RegularSessionNoRealBarYet_SynthesizesTheLiveBarFromSessionMinutes()
  {
    // The exchange's daily bar for today arrives only after the close: the session minutes
    // in the cache are the day's live bar, otherwise the chart loses whole trading days
    var yesterday = Daily(329m, 330m, 328m, 329.5m, 20_000_000, DayAnchor.AddDays(-1));
    var dailies = new[] { yesterday };

    var m1 = DayAnchor.AddHours(9);
    var m2 = DayAnchor.AddHours(10);
    // regular-session minutes: no extended attribution at all
    var gate = CreateGate(Day, DayAnchor, AllDay(m1, m2), null);

    var minutes = new[]
    {
      Minute(m1, 330m, 331m, 329.5m, 330.5m, 50_000),
      Minute(m2, 330.5m, 332m, 330m, 331.5m, 70_000),
    };

    var merged = ExtendedChartComposer.Merge(dailies, minutes, gate.Object, NowInRegular);

    Assert.Equal(2, merged.Length);
    var live = merged[^1];
    Assert.Equal(DayAnchor, DateTime.SpecifyKind(live.Timestamp, DateTimeKind.Utc));
    Assert.Equal(330m, live.Open);
    Assert.Equal(332m, live.High);
    Assert.Equal(329.5m, live.Low);
    Assert.Equal(331.5m, live.Close);
    Assert.Equal(120_000m, live.Volume);
  }

  [Fact]
  public void AfterHours_MergesExtendedMinutesIntoTheRealBar_KeepingItsVolume()
  {
    var regularDay = Daily(330m, 337m, 329m, 336.67m, 30_000_000);
    var dailies = new[] { regularDay };

    var pre = DayAnchor.AddHours(7);
    var regular = DayAnchor.AddHours(10);
    var post = DayAnchor.AddHours(16);
    var gate = CreateGate(Day, DayAnchor, AllDay(pre, regular, post), AllDay(pre, post));

    var minutes = new[]
    {
      Minute(pre, 329m, 330m, 328.5m, 329.5m, 1_000),
      Minute(regular, 330m, 337m, 329m, 336.67m, 29_900_000), // already inside the real bar
      Minute(post, 336.9m, 337.1m, 336.4m, 336.5m, 2_000),
    };

    var merged = ExtendedChartComposer.Merge(dailies, minutes, gate.Object, NowInPostMarket);

    Assert.Single(merged);
    var live = merged[^1];
    Assert.Equal(329m, live.Open); // the pre-market minute opened the bar
    Assert.Equal(337.1m, live.High);
    Assert.Equal(328.5m, live.Low);
    Assert.Equal(336.5m, live.Close); // the newest extended minute
    // the official bar's volume already counts extended trades: re-adding the extended
    // minutes' volume would double-count them
    Assert.Equal(30_000_000m, live.Volume);
    Assert.Equal(DayAnchor, DateTime.SpecifyKind(live.Timestamp, DateTimeKind.Utc));
  }

  [Fact]
  public void RegularMinutesWithTheRealBar_ChangeNothing()
  {
    var regularDay = Daily(330m, 337m, 329m, 336.67m, 30_000_000);
    var dailies = new[] { regularDay };

    var m = DayAnchor.AddHours(10);
    // only regular-session minutes: they are already inside the exchange's own bar
    var gate = CreateGate(Day, DayAnchor, AllDay(m), null);

    var minutes = new[] { Minute(m, 330m, 331m, 329.5m, 330.5m, 50_000) };

    Assert.Same(dailies, ExtendedChartComposer.Merge(dailies, minutes, gate.Object, NowInRegular));
  }

  [Fact]
  public void MinutesOfAnotherDay_DoNotMerge()
  {
    var regularDay = Daily(330m, 337m, 329m, 336.67m, 30_000_000);
    var dailies = new[] { regularDay };

    var otherDayMinute = DayAnchor.AddHours(16);
    var gate = CreateGate(Day, DayAnchor, null, null); // no minute belongs to the forming day

    var minutes = new[] { Minute(otherDayMinute, 336.9m, 337.1m, 336.4m, 336.5m, 2_000) };

    Assert.Same(dailies, ExtendedChartComposer.Merge(dailies, minutes, gate.Object, NowInPostMarket));
  }

  [Fact]
  public void EmptyMinuteWindow_KeepsTheSeriesUntouched()
  {
    var dailies = new[] { Daily(1, 2, 0.5m, 1.5m, 100) };
    var gate = CreateGate(Day, DayAnchor);

    Assert.Same(dailies, ExtendedChartComposer.Merge(dailies, Array.Empty<Candle>(), gate.Object, NowInPreMarket));
  }

  [Fact]
  public void OvernightSession_SynthesizesTheNextTradingDayBar()
  {
    var wednesday = Daily(100, 102, 99, 336.67m, 1_000_000, DayAnchor.AddDays(-1));
    var dailies = new[] { wednesday };

    // Thursday 03:00 UTC: the overnight session (Wednesday evening) prices Thursday's bar
    var o1 = DayAnchor.AddHours(-7);
    var o2 = DayAnchor.AddHours(-3);
    var gate = CreateGate(Day, DayAnchor, AllDay(o1, o2), AllDay(o1, o2));

    var minutes = new[]
    {
      Minute(o1, 336.6m, 336.8m, 336.4m, 336.5m, 200),
      Minute(o2, 336.5m, 336.7m, 336.3m, 336.4m, 100),
    };

    var merged = ExtendedChartComposer.Merge(dailies, minutes, gate.Object, new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc));

    Assert.Equal(2, merged.Length);
    var live = merged[^1];
    Assert.Equal(DayAnchor, DateTime.SpecifyKind(live.Timestamp, DateTimeKind.Utc));
    Assert.Equal(336.6m, live.Open);
    Assert.Equal(336.4m, live.Close);
    Assert.Equal(300m, live.Volume);
  }
}
