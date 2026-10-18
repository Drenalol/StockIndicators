using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Abstract;

public interface IInstrumentSource
{
  Task<IInstrument> GetInstrumentAsync(string ticker, CancellationToken cancellationToken = default);
  Task<HashSet<IInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default);

  IAsyncEnumerable<Candle> GetHistoryAsync(
    string ticker,
    DateTime from,
    DateTime to,
    TimeFrame timeFrame,
    CancellationToken cancellationToken = default
  );
}
