using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

public class WeekAccumulator : DateTimeAccumulator
{
  public WeekAccumulator() => Current = DateTimeFactory(DateTime.UtcNow.AddDays(-TimeFrame.Week.GetMaxPeriod()));

  protected override DateTime Accumulate() => Current = Current.AddDays(7);

  private static DateTime GetCurrentMonday(DateTime dt) =>
    new DateTime(
        dt.Year,
        dt.Month,
        dt.Day,
        0,
        0,
        0,
        DateTimeKind.Utc
      )
      .AddDays(-(dt.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)dt.DayOfWeek - 1));

  public static DateTime DateTimeFactory(DateTime dt) => GetCurrentMonday(dt);
}