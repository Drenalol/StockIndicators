using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

public abstract class DateTimeAccumulator
{
  protected DateTime Current { get; set; }
  protected virtual DateTime EndOffset => DateTime.UtcNow;
  protected abstract DateTime Accumulate();

  public IEnumerable<TResult> ForEach<TResult>(Func<DateTime, TResult?> factory)
  {
    var current = Current;

    do
    {
      var result = factory(current);

      if (result != null)
        yield return result;

      current = Accumulate();
    } while (current <= EndOffset);
  }

  public static DateTimeAccumulator Create(TimeFrame timeFrame) =>
    timeFrame switch
    {
      TimeFrame.Minute => new MinuteAccumulator(),
      TimeFrame.Hour => new HourAccumulator(),
      TimeFrame.Day => new DayAccumulator(),
      TimeFrame.Week => new WeekAccumulator(),
      TimeFrame.Month => new MonthAccumulator(),
      _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
    };
}