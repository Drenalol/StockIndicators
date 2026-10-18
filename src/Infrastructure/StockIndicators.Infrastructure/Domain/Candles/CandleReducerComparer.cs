using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles.Accumulators;

namespace StockIndicators.Infrastructure.Domain.Candles;

public class CandleReducerComparer : IEqualityComparer<Candle>
{
  public static readonly CandleReducerComparer Instance = new();

  public bool Equals(Candle? x, Candle? y) =>
    x != null &&
    y != null &&
    x.Ticker == y.Ticker &&
    x.ExchangeName == y.ExchangeName &&
    x.TimeFrame == y.TimeFrame &&
    x.Timestamp.ToTimeFrameDateTime(x.TimeFrame).Equals(y.Timestamp.ToTimeFrameDateTime(y.TimeFrame));

  public int GetHashCode(Candle obj) => HashCode.Combine(obj.Ticker, obj.ExchangeName, obj.TimeFrame, obj.Timestamp.ToTimeFrameDateTime(obj.TimeFrame));
}
