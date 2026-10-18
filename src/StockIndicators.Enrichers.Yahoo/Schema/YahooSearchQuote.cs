using Newtonsoft.Json;

namespace StockIndicators.Enrichers.Yahoo.Schema;

public class YahooSearchQuote
{
  [JsonProperty("symbol")]
  public string? Symbol { get; set; }

  [JsonProperty("shortname")]
  public string? Shortname { get; set; }

  [JsonProperty("longname")]
  public string? Longname { get; set; }

  [JsonProperty("exchange")]
  public string? Exchange { get; set; }

  [JsonProperty("exchDisp")]
  public string? ExchDisp { get; set; }

  [JsonProperty("quoteType")]
  public string? QuoteType { get; set; }

  [JsonProperty("sector")]
  public string? Sector { get; set; }

  [JsonProperty("industry")]
  public string? Industry { get; set; }
}
