using StockIndicators.Abstract;

namespace StockIndicators.Providers.Tinkoff;

public class TinkoffCandle : ICandle
{
  public string Ticker { get; set; }
  public string? ExchangeName { get; set; }
  public string Provider { get; set; }
  public DateTime Created { get; set; }
  public decimal Open { get; set; }
  public decimal High { get; set; }
  public decimal Low { get; set; }
  public decimal Close { get; set; }
  public decimal Volume { get; set; }

  public TinkoffCandle(
    string ticker,
    string? exchangeName,
    DateTime created,
    decimal open,
    decimal high,
    decimal low,
    decimal close,
    decimal volume
  )
  {
    Ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
    ExchangeName = exchangeName;
    Provider = "Tinkoff";
    Created = created;
    Open = open;
    High = high;
    Low = low;
    Close = close;
    Volume = volume;
  }
}
