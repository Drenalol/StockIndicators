using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Infrastructure.Database;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Restores instruments from active subscriptions at startup so providers
/// (Tinkoff stream, Alpaca poller) resume tracking before processing candles.
/// Hosted services start in registration order, so this must be registered
/// before AddTinkoffProvider / AddAlpacaProvider.
/// </summary>
public class InitInstruments : IHostedService
{
  private readonly InstrumentProvider _instrumentProvider;
  private readonly IServiceScopeFactory _serviceScopeFactory;
  private readonly ILogger _logger;

  public InitInstruments(InstrumentProvider instrumentProvider, IServiceScopeFactory serviceScopeFactory, ILogger logger)
  {
    _instrumentProvider = instrumentProvider;
    _serviceScopeFactory = serviceScopeFactory;
    _logger = logger;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await using var scope = _serviceScopeFactory.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<StockIndicatorsContext>();

    // Subscriptions carry no instrument of their own: everything a rule condition watches
    // through an active subscription must stream live from the start
    var pairs = await context.Subscriptions
      .AsNoTracking()
      .Join(
        context.RuleInstruments.AsNoTracking(),
        subscription => subscription.RuleGroupId,
        instrument => instrument.RuleId,
        (subscription, instrument) => new { subscription.Status, instrument.Ticker, instrument.ExchangeName })
      .Where(row => row.Status == SubscriptionStatus.Active)
      .Select(row => new { row.Ticker, row.ExchangeName })
      .Distinct()
      .ToListAsync(cancellationToken);

    var keys = pairs
      .Select(pair => new InstrumentKey(pair.Ticker, pair.ExchangeName.Length > 0 ? pair.ExchangeName : null))
      .ToList();
    _instrumentProvider.Init(keys);

    _logger.Information("InstrumentProvider restored {Count} instruments from active subscriptions: {@Keys}", keys.Count, keys);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
