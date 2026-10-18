using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Internal;

namespace StockIndicators.Hubs;

/// <summary>
/// Live updates for the chart pages: an open page joins a per-instrument group
/// ("the chart is open" is exactly the group membership) and claims the instrument in
/// the providers' watch list, so the candle pipeline starts polling it even without an
/// active subscription. The pipeline fires a lightweight <c>chartUpdate</c> signal into
/// the groups whose instruments got fresh candles; the client refetches the chart data
/// through the regular chart endpoints, so the hub never carries series. Closed pages
/// leave their groups and release the claim: the group is empty, sends to it are no-ops,
/// and instruments nobody watches are not polled anymore.
/// </summary>
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class ChartHub : Hub
{
  public const string ChartUpdateEvent = "chartUpdate";

  private readonly ChartViewWatchRegistry _watches;

  public ChartHub(ChartViewWatchRegistry watches) => _watches = watches;

  /// <summary>Group identity of an instrument; the same pair (ticker, exchange) the charts use.</summary>
  public static string GroupName(string ticker, string? exchange) =>
    $"tick:{ticker.ToLowerInvariant()}|{exchange?.ToLowerInvariant() ?? string.Empty}";

  // The client contract is the short camelCase names (see useTickerLiveUpdates): SignalR
  // binds hub methods by their exact name and does not trim the Async suffix, so the
  // methods must be named for the wire explicitly
  [HubMethodName("subscribe")]
  public async Task SubscribeAsync(string ticker, string? exchange)
  {
    if (!TryWatchKey(ticker, exchange, out var key))
      return;

    _watches.Add(Context.ConnectionId, key);

    await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(key.Ticker, key.ExchangeName));
  }

  [HubMethodName("unsubscribe")]
  public async Task UnsubscribeAsync(string ticker, string? exchange)
  {
    if (!TryWatchKey(ticker, exchange, out var key))
      return;

    _watches.Remove(Context.ConnectionId, key);

    await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(key.Ticker, key.ExchangeName));
  }

  /// <summary>
  /// Releases every chart-view claim of the connection: navigations and closed tabs
  /// surface here after the transport drops (a reconnecting page claims its instruments
  /// again on the new connection via the client's resubscribe).
  /// </summary>
  public override Task OnDisconnectedAsync(Exception? exception)
  {
    _watches.RemoveConnection(Context.ConnectionId);

    return base.OnDisconnectedAsync(exception);
  }

  /// <summary>Watch identity of a hub call: same case-insensitive normalization the provider uses.</summary>
  private static bool TryWatchKey(string ticker, string? exchange, out InstrumentKey key)
  {
    key = null!;

    if (string.IsNullOrWhiteSpace(ticker) || ticker.Length > 64 || exchange is { Length: > 64 })
      return false;

    var exchangeName = exchange?.ToLowerInvariant();
    key = new InstrumentKey(ticker.ToLowerInvariant(), string.IsNullOrEmpty(exchangeName) ? null : exchangeName);

    return true;
  }
}
