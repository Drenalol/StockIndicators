using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Domain.Extensions;

public static class TimeFrameExtensions
{
  public static int GetMaxPeriod(this TimeFrame timeFrame) => timeFrame.Get<MaxPeriodsAttribute>().Max;

  /// <summary>
  /// Gets the time duration of a single candle for the given TimeFrame.
  /// </summary>
  public static TimeSpan GetCandlePeriod(this TimeFrame timeFrame) => timeFrame switch
  {
    TimeFrame.Minute => TimeSpan.FromMinutes(1),
    TimeFrame.Hour => TimeSpan.FromHours(1),
    TimeFrame.Day => TimeSpan.FromDays(1),
    TimeFrame.Week => TimeSpan.FromDays(7),
    TimeFrame.Month => TimeSpan.FromDays(30),
    _ => TimeSpan.FromMinutes(1)
  };
}
