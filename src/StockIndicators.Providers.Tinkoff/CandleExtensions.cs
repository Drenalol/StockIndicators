using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using StockIndicators.Components.Utils;
using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Infrastructure.Domain.Candles.Accumulators;
using Tinkoff.InvestApi.V1;

namespace StockIndicators.Providers.Tinkoff;

public static class CandleExtensions
{
  private static readonly Calendar Calendar = CultureInfo.GetCultureInfo("ru-RU").Calendar;

  public static CandleInterval ToCandleInterval(this TimeFrame timeFrame) =>
    timeFrame switch
    {
      TimeFrame.Minute => CandleInterval._1Min,
      TimeFrame.Hour => CandleInterval.Hour,
      TimeFrame.Day or TimeFrame.Week or TimeFrame.Month => CandleInterval.Day,
      _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
    };

  public static IEnumerable<HistoricCandle> ReduceCandlesIfNeeded(this IEnumerable<HistoricCandle> candles, TimeFrame timeFrame)
  {
    return timeFrame switch
    {
      TimeFrame.Minute or TimeFrame.Hour or TimeFrame.Day => candles.Distinct(),
      TimeFrame.Week => ReduceWeek(),
      TimeFrame.Month => ReduceMonth(),
      _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
    };

    IEnumerable<HistoricCandle> ReduceWeek() =>
      candles
        .Distinct()
        .Reduce(
          candle =>
          {
            var dateTime = candle.Time.ToDateTime();

            return (Calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday), dateTime.Year);
          },
          WeekAccumulator.DateTimeFactory
        );

    IEnumerable<HistoricCandle> ReduceMonth() =>
      candles
        .Distinct()
        .Reduce(
          candle =>
          {
            var dateTime = candle.Time.ToDateTime();

            return (dateTime.Month, dateTime.Year);
          },
          MonthAccumulator.DateTimeFactory
        );
  }

  private static IEnumerable<HistoricCandle> Reduce<TKey>(this IEnumerable<HistoricCandle> candles, Func<HistoricCandle, TKey> keySelector, Func<DateTime, DateTime> startDateTimeSelector) =>
    candles.GroupBy(
      keySelector,
      (_, grouping) =>
      {
        var groupingCandles = grouping.AsArray();
        var first = groupingCandles[0];
        var last = groupingCandles[^1];

        return new HistoricCandle
        {
          Open = first.Open,
          High = groupingCandles.MaxBy(candle => (decimal)candle.High)!.High,
          Low = groupingCandles.MinBy(candle => (decimal)candle.Low)!.Low,
          Close = last.Close,
          Volume = groupingCandles.Sum(candle => candle.Volume),
          Time = Timestamp.FromDateTime(startDateTimeSelector(first.Time.ToDateTime())),
          IsComplete = groupingCandles.All(candle => candle.IsComplete)
        };
      }
    );

  private static IEnumerable<HistoricCandle> Distinct(this IEnumerable<HistoricCandle> candles) => candles.DistinctBy(candle => candle.Time);
}