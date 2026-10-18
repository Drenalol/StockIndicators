using System.ComponentModel.DataAnnotations;

namespace StockIndicators.Providers.Base;

public class ProviderOptions
{
  public bool IsEnabled { get; set; }

  [Required, MinLength(1)]
  public LimitOption[]? Limits { get; set; }
}

public class LimitOption
{
  public int Value { get; set; }
  public TimeSpan Interval { get; set; }
}