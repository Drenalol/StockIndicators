using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Candles;

public class UpdateCacheCommand : IBackgroundCommand
{
  public IEnumerable<Candle> Candles { get; }

  public UpdateCacheCommand(IEnumerable<Candle> candles) => Candles = candles;
}