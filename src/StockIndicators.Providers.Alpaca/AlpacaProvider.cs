using Alpaca.Markets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockIndicators.Abstract;
using StockIndicators.Abstract.Models;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Base.Exchange;

namespace StockIndicators.Providers.Alpaca;

public class AlpacaProvider : IProvider
{
  public ProviderCode ProviderCode => ProviderCode.Alpaca;
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

  /// <summary>
  /// The minute-poll limiter for the live pipeline. The session gate opens for the extended
  /// (pre/post-market) hours too: extended minute bars are real market data that keeps the
  /// charts and the extended quote live, while alert evaluation on extended candles is
  /// gated separately (StockIndicators.Application.Rules.ITradingSessionGate). Shares the rate
  /// budget with <see cref="ApiLimiter"/>; the deep-night gap (20:00-04:00) still idles.
  /// </summary>
  public ITimeLimiter PollingLimiter { get; }
  public IAlpacaTradingClient TradingClient { get; }
  public IAlpacaDataClient DataClient { get; }

  public AlpacaProvider(
    IOptions<AlpacaProviderOptions> options,
    [FromKeyedServices(ExchangeSessions.Us)] ExchangeSession exchangeSession
  )
  {
    var value = options.Value;
    var limits = value.Limits!;
    var rateLimit = FromMaxCountByIntervalTimeLimiter.CreateConstraint(limits);
    ApiLimiter = new ComposedTimeLimiter(
      new ExchangeSessionAwaitableConstraint(exchangeSession),
      rateLimit);
    PollingLimiter = new ComposedTimeLimiter(
      new ExchangeSessionAwaitableConstraint(exchangeSession, includeExtendedSessions: true),
      rateLimit);
    HistoryLimiter = new ComposedTimeLimiter(
      NoOpAwaitableConstraint.Instance,
      rateLimit);
    InstrumentsLimiter = new ComposedTimeLimiter(
      NoOpAwaitableConstraint.Instance,
      FromMaxCountByIntervalTimeLimiter.CreateConstraint(limits));
    var environment = value.Environment.Equals("Live", StringComparison.OrdinalIgnoreCase)
      ? Environments.Live
      : Environments.Paper;
    var key = new SecretKey(value.ApiKey!, value.ApiSecret!);
    TradingClient = environment.GetAlpacaTradingClient(key);
    DataClient = environment.GetAlpacaDataClient(key);
  }
}
