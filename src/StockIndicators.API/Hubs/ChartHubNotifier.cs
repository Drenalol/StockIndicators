using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Hubs;

/// <summary>
/// Hub-backed implementation of the live chart notifier: sends the <c>chartUpdate</c>
/// signal into the per-instrument group. The candle provider batches every minute and a
/// single instrument may arrive in several consecutive batches, so sends are throttled
/// per group: a page that just refetched will not be asked to refetch again within the
/// throttle window. Delivery failures are swallowed by design (the notifier contract):
/// losing a live signal must never break the candle pipeline behind it.
/// </summary>
public class ChartHubNotifier : IChartLiveNotifier
{
  private static readonly TimeSpan PerInstrumentThrottle = TimeSpan.FromSeconds(10);

  private readonly IHubContext<ChartHub> _hubContext;
  private readonly Serilog.ILogger _logger;
  private readonly ConcurrentDictionary<string, DateTime> _lastSentUtc = new();

  public ChartHubNotifier(IHubContext<ChartHub> hubContext, Serilog.ILogger logger)
  {
    _hubContext = hubContext;
    _logger = logger.ForContext<ChartHubNotifier>();
  }

  public async Task NotifyChangedAsync(IEnumerable<InstrumentKey> instruments, CancellationToken cancellationToken = default)
  {
    foreach (var (ticker, exchangeName) in instruments.Distinct())
    {
      if (string.IsNullOrWhiteSpace(ticker) || ticker.Length > 64)
        continue;

      var group = ChartHub.GroupName(ticker, exchangeName);

      var nowUtc = DateTime.UtcNow;

      if (_lastSentUtc.TryGetValue(group, out var lastSent) && nowUtc - lastSent < PerInstrumentThrottle)
        continue;

      _lastSentUtc[group] = nowUtc;

      try
      {
        await _hubContext.Clients.Group(group).SendAsync(ChartHub.ChartUpdateEvent, new { ticker, exchange = exchangeName }, cancellationToken);
      }
      catch (Exception e)
      {
        _logger.Warning(e, "chartUpdate delivery for {Ticker} ({ExchangeName}) failed", ticker, exchangeName);
      }
    }
  }
}
