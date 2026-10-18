using Microsoft.Extensions.Options;
using StockIndicators.Abstract;
using StockIndicators.Abstract.Models;
using StockIndicators.Providers.Base;
using StockIndicators.Providers.Base.Exchange;

namespace StockIndicators.Providers.Binance;

public class BinanceProvider : IProvider
{
  public ProviderCode ProviderCode => ProviderCode.Binance;
  public ITimeLimiter ApiLimiter { get; }

  public BinanceProvider(IOptions<BinanceProviderOptions> options)
  {
    ApiLimiter = new ComposedTimeLimiter(
      NoOpAwaitableConstraint.Instance,
      FromMaxCountByIntervalTimeLimiter.CreateConstraint(options.Value.Limits!));
  }
}
