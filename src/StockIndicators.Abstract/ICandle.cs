namespace StockIndicators.Abstract;

public interface ICandle
{
  public string Ticker { get; set; }
  public string? ExchangeName { get; set; }
  public DateTime Created { get; set; }
  public decimal Open { get; set; }
  public decimal High { get; set; }
  public decimal Low { get; set; }
  public decimal Close { get; set; }
  public decimal Volume { get; set; }
}
