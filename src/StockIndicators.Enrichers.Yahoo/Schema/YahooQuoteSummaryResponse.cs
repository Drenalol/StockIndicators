using Newtonsoft.Json;

namespace StockIndicators.Enrichers.Yahoo.Schema;

public class YahooQuoteSummaryResponse
{
  [JsonProperty("quoteSummary")]
  public YahooQuoteSummaryWrapper? QuoteSummary { get; set; }

  public class YahooQuoteSummaryWrapper
  {
    [JsonProperty("result")]
    public YahooSummaryProfile[]? Result { get; set; }

    [JsonProperty("error")]
    public YahooQuoteSummaryError? Error { get; set; }
  }

  public class YahooQuoteSummaryError
  {
    [JsonProperty("code")]
    public string? Code { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
  }
}
