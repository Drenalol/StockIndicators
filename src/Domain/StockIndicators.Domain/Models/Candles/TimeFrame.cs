namespace StockIndicators.Domain.Models.Candles;

public enum TimeFrame : byte
{
  [MaxPeriods(5000)]
  Minute = 0,

  [MaxPeriods(1680)]
  Hour,

  [MaxPeriods(2000)]
  Day,

  [MaxPeriods(1500)]
  Week,

  [MaxPeriods(250)]
  Month
}