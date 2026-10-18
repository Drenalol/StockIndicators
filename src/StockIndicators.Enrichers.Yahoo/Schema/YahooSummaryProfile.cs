using Newtonsoft.Json;

namespace StockIndicators.Enrichers.Yahoo.Schema;

public class YahooSummaryProfile
{
  [JsonProperty("country")]
  public string? Country { get; set; }

  [JsonProperty("website")]
  public string? Website { get; set; }

  [JsonProperty("sector")]
  public string? Sector { get; set; }

  [JsonProperty("industry")]
  public string? Industry { get; set; }

  [JsonProperty("longBusinessSummary")]
  public string? LongBusinessSummary { get; set; }

  [JsonProperty("fullTimeEmployees")]
  public int? FullTimeEmployees { get; set; }
}
