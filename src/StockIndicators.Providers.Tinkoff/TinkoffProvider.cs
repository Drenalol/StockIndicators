using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockIndicators.Abstract;
using StockIndicators.Abstract.Models;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Base.Exchange;

namespace StockIndicators.Providers.Tinkoff;

public class TinkoffProvider : IProvider
{
  public ProviderCode ProviderCode => ProviderCode.Tinkoff;
  public readonly ITimeLimiter ApiLimiter;

  /// <summary>
  /// Rate limiting only, no exchange-session gate: the instruments list is reference
  /// data available outside trading hours and must not wait for the session to open.
  /// </summary>
  public readonly ITimeLimiter InstrumentsLimiter;

  /// <summary>
  /// Rate limiting only, no exchange-session gate: history backfill on a user request
  /// path must answer outside trading hours instead of idling until the next session
  /// open. Shares the rate budget with <see cref="ApiLimiter"/>.
  /// </summary>
  public readonly ITimeLimiter HistoryLimiter;

  public TinkoffProvider(
    IOptions<TinkoffProviderOptions> tinkoffOptions,
    [FromKeyedServices(ExchangeSessions.Moex)] ExchangeSession exchangeSession
  )
  {
    var limits = tinkoffOptions.Value.Limits!;
    var rateLimit = FromMaxCountByIntervalTimeLimiter.CreateConstraint(limits);
    ApiLimiter = new ComposedTimeLimiter(
      new ExchangeSessionAwaitableConstraint(exchangeSession),
      rateLimit);
    HistoryLimiter = new ComposedTimeLimiter(
      NoOpAwaitableConstraint.Instance,
      rateLimit);
    InstrumentsLimiter = new ComposedTimeLimiter(
      NoOpAwaitableConstraint.Instance,
      FromMaxCountByIntervalTimeLimiter.CreateConstraint(limits));
  }
}
