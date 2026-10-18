using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockIndicators.Abstract;
using StockIndicators.Abstract.Models;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Base.Exchange;

namespace StockIndicators.Providers.BusinessQuant;

public class BusinessQuantProvider : IProvider
{
  public ProviderCode ProviderCode => ProviderCode.BusinessQuant;
  public ITimeLimiter ApiLimiter { get; }

  /// <summary>
  /// Rate limiting only, no exchange-session gate: the instruments list is reference
  /// data available outside trading hours and must not wait for the session to open.
  /// </summary>
  public ITimeLimiter InstrumentsLimiter { get; }

  /// <summary>
  /// Rate limiting only, no exchange-session gate: history backfill on a user request
  /// path must answer outside trading hours (e.g. extended-hours candles) instead of
  /// idling until the next session open. Shares the rate budget with <see cref="ApiLimiter"/>.
  /// </summary>
  public ITimeLimiter HistoryLimiter { get; }
  public string ApiKey { get; }
  public string BaseUrl { get; }

  public BusinessQuantProvider(
    IOptions<BusinessQuantProviderOptions> options,
    [FromKeyedServices(ExchangeSessions.Us)] ExchangeSession exchangeSession
  )
  {
    var value = options.Value;
    var limits = value.Limits!;
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
    ApiKey = value.ApiKey!;
    BaseUrl = value.BaseUrl;
  }
}
