using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

internal class HourAccumulator : DateTimeAccumulator
{
  public HourAccumulator() => Current = DateTime.UtcNow.AddHours(-TimeFrame.Hour.GetMaxPeriod()).ToTimeFrameDateTime(TimeFrame.Hour);

  protected override DateTime Accumulate() => Current = Current.AddHours(1);

  protected override DateTime EndOffset => base.EndOffset.AddHours(1);
}