using System.Text.Json.Serialization;

namespace StockIndicators.Providers.Binance.Schema;

public class BinanceExchangeInfo
{
  public string? Timezone { get; set; }

  [JsonPropertyName("serverTime")]
  public long ServerTime { get; set; }

  public List<BinanceSymbol> Symbols { get; set; } = [];
}

public class BinanceSymbol
{
  public string Symbol { get; set; } = null!;
  public string Status { get; set; } = null!;

  [JsonPropertyName("baseAsset")]
  public string BaseAsset { get; set; } = null!;

  [JsonPropertyName("quoteAsset")]
  public string QuoteAsset { get; set; } = null!;

  [JsonPropertyName("baseAssetPrecision")]
  public int BaseAssetPrecision { get; set; }

  [JsonPropertyName("quoteAssetPrecision")]
  public int QuoteAssetPrecision { get; set; }

  [JsonPropertyName("isSpotTradingAllowed")]
  public bool IsSpotTradingAllowed { get; set; }

  [JsonPropertyName("isMarginTradingAllowed")]
  public bool IsMarginTradingAllowed { get; set; }
}
