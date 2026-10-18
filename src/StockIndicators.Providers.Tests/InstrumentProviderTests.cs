using Serilog;
using Serilog.Core;
using Serilog.Events;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Infrastructure.Domain.Instruments;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class InstrumentProviderTests
{
  [Fact]
  public void AddWatch_SameSourceTwice_FiresOnAddedOnce()
  {
    var provider = new InstrumentProvider(Logger.None);
    var added = new List<InstrumentKey>();
    using var _ = provider.OnAdded.Subscribe(added.Add);

    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);
    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);

    Assert.Equal(new[] { Key("AAPL", "NASDAQ") }, added);
  }

  [Fact]
  public void RemoveWatch_KeepsInstrument_UntilLastSourceLeaves()
  {
    var provider = new InstrumentProvider(Logger.None);
    var added = new List<InstrumentKey>();
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnAdded.Subscribe(added.Add);
    using var __ = provider.OnRemoved.Subscribe(removed.Add);

    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);
    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);

    Assert.Single(added);
    Assert.Empty(removed);

    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);

    Assert.Empty(removed);
    Assert.Contains("AAPL", provider.Instruments);

    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);

    Assert.Equal(new[] { Key("AAPL", "NASDAQ") }, removed);
    Assert.DoesNotContain("AAPL", provider.Instruments);
  }

  [Fact]
  public void RemoveWatch_UnknownKeyOrSource_IsNoOp()
  {
    var provider = new InstrumentProvider(Logger.None);
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnRemoved.Subscribe(removed.Add);

    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);

    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);
    provider.RemoveWatch(Key("MSFT", "NASDAQ"), InstrumentWatchSource.Subscription);

    Assert.Empty(removed);
    Assert.Contains("AAPL", provider.Instruments);
  }

  [Fact]
  public void RemoveWatch_KeepsTicker_WhileAnotherExchangeRemains()
  {
    var provider = new InstrumentProvider(Logger.None);
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnRemoved.Subscribe(removed.Add);

    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);
    provider.AddWatch(Key("AAPL", "MOEX"), InstrumentWatchSource.ChartView);

    // The NASDAQ key loses its only source: its stream stops, but the shared ticker
    // stays watched through MOEX
    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);

    Assert.Equal(new[] { Key("AAPL", "NASDAQ") }, removed);
    Assert.Contains("AAPL", provider.Instruments);

    provider.RemoveWatch(Key("AAPL", "MOEX"), InstrumentWatchSource.ChartView);

    Assert.Equal(new[] { Key("AAPL", "NASDAQ"), Key("AAPL", "MOEX") }, removed);
    Assert.DoesNotContain("AAPL", provider.Instruments);
  }

  [Fact]
  public void Watch_PairsAcrossSources_CaseInsensitively_EmitsKeyAsGiven()
  {
    var provider = new InstrumentProvider(Logger.None);
    var added = new List<InstrumentKey>();
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnAdded.Subscribe(added.Add);
    using var __ = provider.OnRemoved.Subscribe(removed.Add);

    // The subscription path passes lower-cased match keys, the hub passes the canonical casing
    provider.AddWatch(Key("aapl", "nasdaq"), InstrumentWatchSource.Subscription);
    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);

    Assert.Single(added);

    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);
    provider.RemoveWatch(Key("aapl", "nasdaq"), InstrumentWatchSource.Subscription);

    // The last release carries the key exactly as the caller passed it
    Assert.Equal(new[] { Key("aapl", "nasdaq") }, removed);
    Assert.Empty(provider.Keys);
  }

  [Fact]
  public void Init_RegistersKeys_WithoutEvents()
  {
    var provider = new InstrumentProvider(Logger.None);
    var events = new List<string>();
    using var _ = provider.OnAdded.Subscribe(key => events.Add($"added:{key.Ticker}"));
    using var __ = provider.OnRemoved.Subscribe(key => events.Add($"removed:{key.Ticker}"));

    provider.Init([Key("AAPL", "NASDAQ")]);

    Assert.Empty(events);
    Assert.Contains("AAPL", provider.Instruments);

    // A viewer arriving later keeps the instrument when the restored subscription leaves
    provider.AddWatch(Key("aapl", "nasdaq"), InstrumentWatchSource.ChartView);
    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);

    Assert.Empty(events);

    provider.RemoveWatch(Key("aapl", "nasdaq"), InstrumentWatchSource.ChartView);

    // The release carries the key exactly as its last viewer passed it
    Assert.Equal(new[] { "removed:aapl" }, events);
  }

  [Fact]
  public void Keys_NormalizeIdentity_EmptyExchangeBecomesNull()
  {
    var provider = new InstrumentProvider(Logger.None);

    provider.AddWatch(Key("BTCUSDT", ""), InstrumentWatchSource.ChartView);
    provider.AddWatch(Key("btcusdt", null), InstrumentWatchSource.Subscription);

    Assert.Equal(new[] { Key("btcusdt", null) }, provider.Keys);

    provider.RemoveWatch(Key("BTCUSDT", ""), InstrumentWatchSource.ChartView);
    provider.RemoveWatch(Key("BTCUSDT", null), InstrumentWatchSource.Subscription);

    Assert.Empty(provider.Keys);
  }

  [Fact]
  public void Transitions_LogOnlyRealWatchListEnterAndExit_WithTheSource()
  {
    var events = new List<LogEvent>();
    var logger = new LoggerConfiguration().WriteTo.Sink(new CollectingSink(events)).CreateLogger();
    var provider = new InstrumentProvider(logger);

    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);
    // the second source does not re-enter the watch list: no second line
    provider.AddWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);
    // the first release keeps the other source watching: no exit line yet
    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.Subscription);
    provider.RemoveWatch(Key("AAPL", "NASDAQ"), InstrumentWatchSource.ChartView);
    // releases of keys that were never watched log nothing
    provider.RemoveWatch(Key("MSFT", "NASDAQ"), InstrumentWatchSource.ChartView);

    // RenderMessage() quotes string scalars ("AAPL"); the quotes are a Serilog rendering
    // artifact, so the assertion pins the readable line without them
    var messages = events.Select(e => e.RenderMessage().Replace("\"", "")).ToArray();

    Assert.Equal(
      new[]
      {
        "Watch list: AAPL (NASDAQ) started watching via Subscription",
        "Watch list: AAPL (NASDAQ) stopped watching (last source released by ChartView)",
      },
      messages);
  }

  private sealed class CollectingSink(List<LogEvent> events) : Serilog.Core.ILogEventSink
  {
    public void Emit(LogEvent logEvent)
    {
      lock (events)
        events.Add(logEvent);
    }
  }

  private static InstrumentKey Key(string ticker, string? exchange) => new(ticker, exchange);
}
