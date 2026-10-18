using System.Collections.Concurrent;
using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Hubs;

/// <summary>
/// Chart-view membership of the provider watch list: an open chart page claims its
/// instrument under the <see cref="InstrumentWatchSource.ChartView"/> source, so the
/// candle providers poll it even without a single active subscription; the claim is
/// released when the page unsubscribes or its hub connection goes away. Several pages
/// can watch the same instrument, so the registry counts viewers per key and touches
/// the provider only on the 0↔1 transitions. SignalR groups are not enumerable, so the
/// registry keeps its own connection -> keys map for the disconnect cleanup; a
/// reconnecting page claims again on its new connection while the stale one is
/// released through OnDisconnected.
/// </summary>
public sealed class ChartViewWatchRegistry
{
  private readonly IInstrumentProvider _instrumentProvider;
  private readonly ConcurrentDictionary<string, ConcurrentDictionary<InstrumentKey, byte>> _keysByConnection = new();
  private readonly ConcurrentDictionary<InstrumentKey, int> _viewersByKey = new();

  public ChartViewWatchRegistry(IInstrumentProvider instrumentProvider) => _instrumentProvider = instrumentProvider;

  public void Add(string connectionId, InstrumentKey key)
  {
    var keys = _keysByConnection.GetOrAdd(connectionId, _ => new ConcurrentDictionary<InstrumentKey, byte>());

    if (!keys.TryAdd(key, 0))
      return;

    _viewersByKey.AddOrUpdate(key, 1, (_, count) => count + 1);
    _instrumentProvider.AddWatch(key, InstrumentWatchSource.ChartView);
  }

  public void Remove(string connectionId, InstrumentKey key)
  {
    if (_keysByConnection.TryGetValue(connectionId, out var keys) && keys.TryRemove(key, out _))
      Release(key);
  }

  public void RemoveConnection(string connectionId)
  {
    if (!_keysByConnection.TryRemove(connectionId, out var keys))
      return;

    foreach (var key in keys.Keys)
      Release(key);
  }

  private void Release(InstrumentKey key)
  {
    var viewers = _viewersByKey.AddOrUpdate(key, 0, (_, count) => count - 1);

    if (viewers > 0)
      return;

    _viewersByKey.TryRemove(key, out _);
    _instrumentProvider.RemoveWatch(key, InstrumentWatchSource.ChartView);
  }
}
