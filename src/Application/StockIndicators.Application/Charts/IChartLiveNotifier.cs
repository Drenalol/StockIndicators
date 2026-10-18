using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Tells the chart pages that an instrument's candle data has just changed, so an open
/// chart can refetch its series. Only the signal travels: the chart data itself is
/// produced by the existing chart endpoints, and instruments nobody watches have no
/// subscribers at all (the live hub groups are joined by the open pages only).
/// Best-effort by contract: implementations must never let a delivery failure break the
/// candle pipeline that calls them.
/// </summary>
public interface IChartLiveNotifier
{
  Task NotifyChangedAsync(IEnumerable<InstrumentKey> instruments, CancellationToken cancellationToken = default);
}
