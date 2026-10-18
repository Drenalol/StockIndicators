using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

internal class MinuteAccumulator : DateTimeAccumulator
{
  public MinuteAccumulator() => Current = DateTime.UtcNow.AddMinutes(-TimeFrame.Minute.GetMaxPeriod()).ToTimeFrameDateTime(TimeFrame.Minute);

  protected override DateTime Accumulate() => Current = Current.AddMinutes(1);
}