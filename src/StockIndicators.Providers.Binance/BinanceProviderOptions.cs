using System.ComponentModel.DataAnnotations;
using StockIndicators.Providers.Base;

namespace StockIndicators.Providers.Binance;

public class BinanceProviderOptions : ProviderOptions
{
  public string BaseUrl { get; set; } = "https://api.binance.com";

  /// <summary>Geo-mirror used when the primary host is blocked (451/403).</summary>
  public string? MirrorBaseUrl { get; set; }

  [Range(5, 3600)]
  public int CandlePollIntervalSeconds { get; set; } = 60;
}
