namespace StockIndicators.Domain.Models.Candles;

public record struct CandleResult(DateTime DateTime, Candle? Candle);

public interface ICandleCache
{
  Task AddRangeAsync(IEnumerable<Candle> candles, bool isRealTime, CancellationToken cancellationToken = default);
  Task<IEnumerable<CandleResult>> GetLastAsync(string ticker, string? exchangeName, TimeFrame timeframe, CancellationToken cancellationToken = default);
}
