using System.Text.Json;
using Refit;
using StockIndicators.Providers.Binance.Schema;

namespace StockIndicators.Providers.Binance.Api;

public interface IBinanceApi
{
  [Get("/api/v3/klines")]
  Task<List<JsonElement[]>> GetKlinesAsync(
    [Query] string symbol,
    [Query] string interval,
    [Query] int limit,
    [Query] long? startTime = null,
    [Query] long? endTime = null,
    CancellationToken cancellationToken = default);

  [Get("/api/v3/exchangeInfo")]
  Task<BinanceExchangeInfo> GetExchangeInfoAsync(CancellationToken cancellationToken = default);

  [Get("/api/v3/ticker/price")]
  Task<BinancePrice> GetPriceAsync([Query] string symbol, CancellationToken cancellationToken = default);
}
