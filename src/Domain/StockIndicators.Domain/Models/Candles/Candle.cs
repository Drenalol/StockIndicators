using FacioQuo.Stock.Indicators;

namespace StockIndicators.Domain.Models.Candles;

public class Candle : Entity, IBar
{
  public string Ticker { get; set; }
  public string? ExchangeName { get; set; }
  public DateTime Timestamp { get; set; }
  public TimeFrame TimeFrame { get; set; }
  public decimal Open { get; set; }
  public decimal High { get; set; }
  public decimal Low { get; set; }
  public decimal Close { get; set; }
  public decimal Volume { get; set; }
  public bool IsNotEmpty { get; set; }
  public bool IsCompleted { get; set; }
  public DateTime Updated { get; set; }

  public double Value => (double)Close;

  public Candle(
    string ticker,
    DateTime date,
    TimeFrame timeFrame,
    decimal open,
    decimal high,
    decimal low,
    decimal close,
    decimal volume,
    bool isNotEmpty = true,
    bool isCompleted = false,
    string? exchangeName = null
  )
  {
    Ticker = ticker;
    ExchangeName = exchangeName;
    Timestamp = date;
    TimeFrame = timeFrame;
    Open = open;
    High = high;
    Low = low;
    Close = close;
    Volume = volume;
    IsNotEmpty = isNotEmpty;
    IsCompleted = isCompleted;
    Updated = DateTime.UtcNow;
  }

  public void Update(Candle candle)
  {
    if (candle.High > High)
      High = candle.High;

    if (candle.Low < Low)
      Low = candle.Low;
    
    Close = candle.Close;
    Volume += candle.Volume;
    Updated = DateTime.UtcNow;

    if (IsCompleted)
      IsCompleted = false;
  }

  public static Candle Empty(string ticker, DateTime created, TimeFrame timeFrame, string? exchangeName = null) =>
    new(
      ticker,
      created,
      timeFrame,
      default,
      default,
      default,
      default,
      default,
      false,
      false,
      exchangeName
    )
    {
      Updated = DateTime.UtcNow
    };
}
