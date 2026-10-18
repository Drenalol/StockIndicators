namespace StockIndicators.Providers.Base.Exchange;

public class ExchangeSessionWindow
{
  public string? Name { get; set; }
  public TimeSpan Open { get; set; }
  public TimeSpan Close { get; set; }
}
