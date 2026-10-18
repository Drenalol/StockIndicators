using StockIndicators.Domain;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Infrastructure.Domain.Candles.Accumulators;

public class MonthAccumulator : DateTimeAccumulator
{
  public MonthAccumulator() => Current = DateTimeFactory(DateTime.UtcNow.AddMonths(-TimeFrame.Month.GetMaxPeriod()));

  protected override DateTime Accumulate() => Current = Current.AddMonths(1);

  public static DateTime DateTimeFactory(DateTime dt) =>
    new(
      dt.Year,
      dt.Month,
      1,
      0,
      0,
      0,
      0,
      DateTimeKind.Utc
    );
}