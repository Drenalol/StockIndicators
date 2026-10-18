using StockIndicators.Providers.Alpaca;
using StockIndicators.Providers.Base.Exchange;
using Xunit;

namespace StockIndicators.Providers.Tests;

/// <summary>
/// The minute-poll window must self-extend over the minutes a stalled pipeline missed:
/// with the old fixed [now-19, now-16] span, every failed cycle left its minutes out of
/// the cache forever (the next window had already moved past them), which is exactly how
/// a 15-minute Alpaca outage yesterday turned into a permanent hole in the minute series.
/// </summary>
public class AlpacaPollWindowTests
{
  private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
  private static readonly TimeSpan Delay = AlpacaProviderConsumerProducer.PollWindowEndOffset;
  private static readonly TimeSpan Overlap = AlpacaProviderConsumerProducer.PollWindowOverlap;

  [Fact]
  public void FirstCycle_WindowIsTheShortDelayedSpan()
  {
    var (start, end) = AlpacaProviderConsumerProducer.ComputePollWindow(Now, default);

    Assert.Equal(Now - Delay, end);
    Assert.Equal(end - Overlap, start);
  }

  [Fact]
  public void NormalCycle_WindowStartsThreeMinutesBehindThePreviousEnd()
  {
    var lastEnd = Now - Delay - TimeSpan.FromMinutes(1);

    var (start, end) = AlpacaProviderConsumerProducer.ComputePollWindow(Now, lastEnd);

    Assert.Equal(Now - Delay, end);
    Assert.Equal(lastEnd - Overlap, start);
  }

  [Fact]
  public void StalledPipeline_WindowExtendsOverTheMissedMinutes()
  {
    var lastEnd = Now - Delay - TimeSpan.FromMinutes(15);

    var (start, end) = AlpacaProviderConsumerProducer.ComputePollWindow(Now, lastEnd);

    // The whole 15-minute stall is re-covered, overlap included
    Assert.Equal(lastEnd - Overlap, start);
    Assert.Equal(TimeSpan.FromMinutes(18), end - start);
  }

  [Fact]
  public void VeryOldLastEnd_WindowIsCappedAtTheHealSpan()
  {
    var lastEnd = Now.AddDays(-1);

    var (start, end) = AlpacaProviderConsumerProducer.ComputePollWindow(Now, lastEnd);

    // Older holes belong to the request-path backfill, not to one giant poll request
    Assert.Equal(end - AlpacaProviderConsumerProducer.PollWindowMaxHeal - Overlap, start);
  }

  // A 200-OK page with no bars in the middle of an open session is an upstream stall, not
  // an empty market: during the Alpaca vendor outage of 2026-10-15 the API served empty
  // pages for an hour and every such cycle consumed its window, turning the stall into a
  // hole the self-healing span never re-covered.
  private static ExchangeSession CreateUsSession() => new(new ExchangeSessionOptions
  {
    TimeZoneId = "America/New_York",
    Sessions = [new ExchangeSessionWindow { Name = "Regular", Open = new TimeSpan(9, 30, 0), Close = new TimeSpan(16, 0, 0) }],
    PreMarket = new ExchangeSessionWindow { Name = "PreMarket", Open = new TimeSpan(4, 0, 0), Close = new TimeSpan(9, 30, 0) },
    PostMarket = new ExchangeSessionWindow { Name = "PostMarket", Open = new TimeSpan(16, 0, 0), Close = new TimeSpan(20, 0, 0) },
    Overnight = new ExchangeSessionWindow { Name = "Overnight", Open = new TimeSpan(20, 0, 0), Close = new TimeSpan(4, 0, 0) }
  });

  [Fact]
  public void EmptyWindowInsideRegularSession_KeepsTheHealMarker()
  {
    // Wednesday 2026-10-14, 13:00-13:04 EDT (UTC-4) - the 2026-10-15 outage shape
    var start = new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc);

    Assert.False(AlpacaProviderConsumerProducer.ShouldConsumePollWindow(CreateUsSession(), start, start.AddMinutes(4), 0));
  }

  [Fact]
  public void EmptyWindowInsideExtendedSession_KeepsTheHealMarker()
  {
    // Wednesday 2026-10-14, 17:30 EDT is post-market: extended bars are real bars
    var start = new DateTime(2026, 10, 14, 21, 30, 0, DateTimeKind.Utc);

    Assert.False(AlpacaProviderConsumerProducer.ShouldConsumePollWindow(CreateUsSession(), start, start.AddMinutes(4), 0));
  }

  [Fact]
  public void EmptyWindowInsideOvernightSession_KeepsTheHealMarker()
  {
    // Thursday 2026-10-15 21:00 EDT (Friday 01:00 UTC) is the BOATS overnight session
    var start = new DateTime(2026, 10, 16, 1, 0, 0, DateTimeKind.Utc);

    Assert.False(AlpacaProviderConsumerProducer.ShouldConsumePollWindow(CreateUsSession(), start, start.AddMinutes(4), 0));
  }

  [Fact]
  public void EmptyWindowStraddlingTheClose_KeepsTheHealMarker()
  {
    // Friday 2026-10-16: 19:50 EDT is post-market, 20:10 EDT is dark (Friday nights have
    // no overnight session) - the window still covers minutes that must hold bars
    var start = new DateTime(2026, 10, 16, 23, 50, 0, DateTimeKind.Utc);

    Assert.False(AlpacaProviderConsumerProducer.ShouldConsumePollWindow(CreateUsSession(), start, start.AddMinutes(20), 0));
  }

  [Fact]
  public void EmptyWindowOutsideAnySession_IsConsumed()
  {
    // Saturday 13:00 EDT: empty is simply the market being closed, the marker moves on
    var start = new DateTime(2026, 10, 17, 17, 0, 0, DateTimeKind.Utc);

    Assert.True(AlpacaProviderConsumerProducer.ShouldConsumePollWindow(CreateUsSession(), start, start.AddMinutes(4), 0));
  }

  [Fact]
  public void NonEmptyWindowInsideSession_IsConsumed()
  {
    var start = new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc);

    Assert.True(AlpacaProviderConsumerProducer.ShouldConsumePollWindow(CreateUsSession(), start, start.AddMinutes(4), 8));
  }
}
