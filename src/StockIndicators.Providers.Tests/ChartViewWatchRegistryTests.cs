using Serilog.Core;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Hubs;
using StockIndicators.Infrastructure.Domain.Instruments;
using Xunit;

namespace StockIndicators.Providers.Tests;

public class ChartViewWatchRegistryTests
{
  [Fact]
  public void Add_ClaimsInstrument_SoProvidersPollIt()
  {
    var provider = new InstrumentProvider(Logger.None);
    var watches = new ChartViewWatchRegistry(provider);
    var added = new List<InstrumentKey>();
    using var _ = provider.OnAdded.Subscribe(added.Add);

    watches.Add("conn-1", Key("AAPL", "NASDAQ"));

    // The hub normalizes the key before claiming; the registry hands it to the provider as given
    Assert.Equal(new[] { Key("AAPL", "NASDAQ") }, added);
    Assert.Contains("AAPL", provider.Instruments);
  }

  [Fact]
  public void Remove_ReleasesTheClaim_Once()
  {
    var provider = new InstrumentProvider(Logger.None);
    var watches = new ChartViewWatchRegistry(provider);
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnRemoved.Subscribe(removed.Add);

    watches.Add("conn-1", Key("AAPL", "NASDAQ"));

    watches.Remove("conn-1", Key("AAPL", "NASDAQ"));
    watches.Remove("conn-1", Key("AAPL", "NASDAQ"));
    watches.Remove("conn-2", Key("AAPL", "NASDAQ"));

    Assert.Single(removed);
  }

  [Fact]
  public void RemoveConnection_KeepsInstrument_WhileAnotherConnectionWatchesIt()
  {
    var provider = new InstrumentProvider(Logger.None);
    var watches = new ChartViewWatchRegistry(provider);
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnRemoved.Subscribe(removed.Add);

    // Two open charts of the same instrument: a reconnect leaves the stale claim to
    // OnDisconnected while the new connection holds its own
    watches.Add("conn-1", Key("AAPL", "NASDAQ"));
    watches.Add("conn-2", Key("AAPL", "NASDAQ"));

    watches.RemoveConnection("conn-1");

    Assert.Empty(removed);
    Assert.Contains("AAPL", provider.Instruments);

    watches.RemoveConnection("conn-2");

    Assert.Single(removed);
    Assert.DoesNotContain("AAPL", provider.Instruments);
  }

  [Fact]
  public void RemoveConnection_ReleasesAllKeysOfTheConnection()
  {
    var provider = new InstrumentProvider(Logger.None);
    var watches = new ChartViewWatchRegistry(provider);

    watches.Add("conn-1", Key("AAPL", "NASDAQ"));
    watches.Add("conn-1", Key("SBER", "MOEX"));
    watches.Add("conn-2", Key("BTCUSDT", null));

    watches.RemoveConnection("conn-1");

    Assert.DoesNotContain("AAPL", provider.Instruments);
    Assert.DoesNotContain("SBER", provider.Instruments);
    Assert.Contains("BTCUSDT", provider.Instruments);
  }

  [Fact]
  public void ChartView_DoesNotReleaseSubscriptionClaims()
  {
    var provider = new InstrumentProvider(Logger.None);
    var watches = new ChartViewWatchRegistry(provider);
    var removed = new List<InstrumentKey>();
    using var _ = provider.OnRemoved.Subscribe(removed.Add);

    provider.AddWatch(Key("aapl", "nasdaq"), InstrumentWatchSource.Subscription);
    watches.Add("conn-1", Key("AAPL", "NASDAQ"));

    watches.RemoveConnection("conn-1");

    Assert.Empty(removed);
    Assert.Contains("AAPL", provider.Instruments);
  }

  private static InstrumentKey Key(string ticker, string? exchange) => new(ticker, exchange);
}
