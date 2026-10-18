using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

internal class DayAccumulator : DateTimeAccumulator
{
  public DayAccumulator() => Current = DateTime.UtcNow.AddDays(-TimeFrame.Day.GetMaxPeriod()).ToTimeFrameDateTime(TimeFrame.Day);

  protected override DateTime Accumulate() => Current = Current.AddDays(1);
}