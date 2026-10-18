using StockIndicators.Providers.Binance.Api;

namespace StockIndicators.Providers.Binance.Internal;

public sealed class BinanceApiClients
{
  public IBinanceApi Primary { get; }
  public IBinanceApi Mirror { get; }

  public BinanceApiClients(IBinanceApi primary, IBinanceApi mirror)
  {
    Primary = primary;
    Mirror = mirror;
  }
}
