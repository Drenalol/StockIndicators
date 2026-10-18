using System.ComponentModel.DataAnnotations;

namespace StockIndicators.Enrichers.Yahoo;

public class YahooEnricherOptions
{
  public bool IsEnabled { get; set; }

  [Required, Url]
  public string? BaseUrl { get; set; } = "https://query1.finance.yahoo.com";

  [Range(1, 720)]
  public int CacheTtlHours { get; set; } = 24;

  [Range(1, 720)]
  public int NegativeCacheTtlHours { get; set; } = 24;

  /// <summary>Fetch deep company profile (country, website, summary) via cookie + crumb flow.</summary>
  public bool DeepProfile { get; set; }
}
