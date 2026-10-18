using System.ComponentModel.DataAnnotations;
using StockIndicators.Providers.Base;

namespace StockIndicators.Providers.Alpaca;

public class AlpacaProviderOptions : ProviderOptions
{
  [Required]
  public string? ApiKey { get; set; }

  [Required]
  public string? ApiSecret { get; set; }

  /// <summary>Paper or Live</summary>
  public string Environment { get; set; } = "Paper";

  [Range(5, 3600)]
  public int CandlePollIntervalSeconds { get; set; } = 60;
}
