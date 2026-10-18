using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

internal static class StrategyDateTimeMapper
{
  public static DateTime ToTimeFrameDateTime(this DateTime dt, TimeFrame timeFrame) =>
    timeFrame switch
    {
      TimeFrame.Minute => new DateTime(
        dt.Year,
        dt.Month,
        dt.Day,
        dt.Hour,
        dt.Minute,
        0,
        DateTimeKind.Utc
      ),
      TimeFrame.Hour => new DateTime(
        dt.Year,
        dt.Month,
        dt.Day,
        dt.Hour,
        0,
        0,
        DateTimeKind.Utc
      ),
      TimeFrame.Day or TimeFrame.Week or TimeFrame.Month => new DateTime(
        dt.Year,
        dt.Month,
        dt.Day,
        0,
        0,
        0,
        DateTimeKind.Utc
      ),
      _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
    };
}