namespace StockIndicators.Domain.Models.Candles;

public interface ICandleRepository
{
  Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default);

  Task<Candle[]> GetLastCandlesAsync(
    string ticker,
    string? exchangeName,
    int periods,
    TimeFrame timeFrame,
    CancellationToken cancellationToken = default
  );

  Task<Candle[]> GetCandlesAsync(
    string ticker,
    string? exchangeName,
    DateTime start,
    DateTime end,
    TimeFrame timeFrame,
    CancellationToken cancellationToken = default
  );
}
