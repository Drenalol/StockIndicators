using System.ComponentModel.DataAnnotations;
using StockIndicators.Providers.Base;

namespace StockIndicators.Providers.BusinessQuant;

public class BusinessQuantProviderOptions : ProviderOptions
{
  [Required]
  public string? ApiKey { get; set; }

  public string BaseUrl { get; set; } = "https://data.businessquant.com";

  [Range(5, 3600)]
  public int CandlePollIntervalSeconds { get; set; } = 60;
}
