namespace StockIndicators.Providers.Binance.Schema;

public class BinancePrice
{
  public string Symbol { get; set; } = null!;
  public string Price { get; set; } = null!;
  public long CloseTime { get; set; }
}
