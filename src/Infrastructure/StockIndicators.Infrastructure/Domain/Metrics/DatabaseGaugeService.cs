using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Metrics;

/// <summary>
/// Refreshes the cheap database counts behind the <c>si_*</c> observable gauges every
/// 30 seconds (the scrape callbacks only read the last snapshot). A failed refresh keeps
/// the previous snapshot and logs a warning; the gauges then simply stop moving.
/// Runs in the single deployed API process.
/// </summary>
public class DatabaseGaugeService : BackgroundService
{
  private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger _logger;

  public DatabaseGaugeService(IServiceScopeFactory scopeFactory, ILogger logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RefreshAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        return;
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Database gauge refresh failed; keeping the previous snapshot");
      }

      try
      {
        await Task.Delay(RefreshInterval, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }
    }
  }

  private async Task RefreshAsync(CancellationToken cancellationToken)
  {
    using var scope = _scopeFactory.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<StockIndicatorsContext>();

    var users = await context.Users.CountAsync(cancellationToken);

    var subscriptionsByStatus = await context.Subscriptions
      .GroupBy(subscription => subscription.Status)
      .Select(group => new { group.Key, Count = (long)group.Count() })
      .ToListAsync(cancellationToken);

    var pushByPlatform = await context.PushSubscriptions
      .GroupBy(push => push.Platform)
      .Select(group => new { group.Key, Count = (long)group.Count() })
      .ToListAsync(cancellationToken);

    // Every label of a gauge must stay present between refreshes, otherwise the series
    // disappears from Prometheus and alerts on "no rows for a status" would fire
    DatabaseGaugeMetrics.SetSnapshot(
      new DatabaseGaugeSnapshot(
        users,
        ToFullMatrix(subscriptionsByStatus.ToDictionary(pair => pair.Key, pair => pair.Count)),
        ToFullMatrix(pushByPlatform.ToDictionary(pair => pair.Key, pair => pair.Count))));
  }

  /// <summary>Every enum member gets a row (zero when nothing matched), so each label series stays present between scrapes.</summary>
  private static Dictionary<TEnum, long> ToFullMatrix<TEnum>(Dictionary<TEnum, long> counts)
    where TEnum : struct, Enum
  {
    var full = new Dictionary<TEnum, long>();

    foreach (var value in Enum.GetValues<TEnum>())
      full[value] = counts.GetValueOrDefault(value);

    return full;
  }
}
