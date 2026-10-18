using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using StockIndicators.Hubs;
using Xunit;

namespace StockIndicators.Providers.Tests;

/// <summary>
/// SignalR binds hub methods by their exact name (the Async suffix is NOT trimmed):
/// the frontend invokes "subscribe"/"unsubscribe", and a drift here breaks every live
/// chart update silently, because the client swallows the invoke rejection.
/// </summary>
public class ChartHubContractTests
{
  [Theory]
  [InlineData(nameof(ChartHub.SubscribeAsync), "subscribe")]
  [InlineData(nameof(ChartHub.UnsubscribeAsync), "unsubscribe")]
  public void HubMethods_ExposeTheWireNamesTheClientInvokes(string clrName, string wireName)
  {
    var method = typeof(ChartHub).GetMethod(clrName, BindingFlags.Public | BindingFlags.Instance);

    Assert.NotNull(method);
    Assert.Equal(wireName, method!.GetCustomAttribute<HubMethodNameAttribute>()?.Name);
  }
}
