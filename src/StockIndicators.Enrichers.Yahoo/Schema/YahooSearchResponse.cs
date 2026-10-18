using Newtonsoft.Json;

namespace StockIndicators.Enrichers.Yahoo.Schema;

public class YahooSearchResponse
{
  [JsonProperty("count")]
  public int Count { get; set; }

  [JsonProperty("quotes")]
  public YahooSearchQuote[] Quotes { get; set; } = [];
}
