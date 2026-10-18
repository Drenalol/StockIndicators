namespace StockIndicators.Domain.Models.Candles;

public interface ICandleProvider
{
  Task<bool> PushAsync(Candle candle, CancellationToken cancellationToken = default);
}