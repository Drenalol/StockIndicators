using System.Text.Json.Serialization;

namespace StockIndicators.Providers.Alpaca.Schema;

/// <summary>
/// One BOATS bar of the multi-symbol bars response (the market data API's compact wire
/// shape: t/o/h/l/c/v/n/vw). Boats is the Blue Ocean ATS feed: the overnight session data
/// the SIP/IEX feeds never carry.
/// </summary>
public sealed class BoatsBar
{
  [JsonPropertyName("t")]
  public DateTime TimeUtc { get; set; }

  [JsonPropertyName("o")]
  public decimal Open { get; set; }

  [JsonPropertyName("h")]
  public decimal High { get; set; }

  [JsonPropertyName("l")]
  public decimal Low { get; set; }

  [JsonPropertyName("c")]
  public decimal Close { get; set; }

  [JsonPropertyName("v")]
  public decimal Volume { get; set; }

  [JsonPropertyName("n")]
  public long TradeCount { get; set; }
}

/// <summary>Multi-symbol bars response: symbol → bars, plus the paging token.</summary>
public sealed class BoatsBarsResponse
{
  [JsonPropertyName("bars")]
  public Dictionary<string, List<BoatsBar>>? Bars { get; set; }

  [JsonPropertyName("next_page_token")]
  public string? NextPageToken { get; set; }
}
