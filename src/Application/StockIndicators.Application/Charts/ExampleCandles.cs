using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Deterministic synthetic OHLCV series for indicator example charts: a seeded random
/// walk with a slow sinusoidal trend so moving averages, bands and oscillators have
/// visible structure. The seed is derived from the indicator + timeframe and the dates
/// are aligned to period boundaries, so the same chart renders identically on every
/// request and process. This is not market data.
/// </summary>
public static class ExampleCandles
{
  private const string Ticker = "EXAMPLE";

  /// <summary>Gap magnitude injected into the Gap example so the alert condition is visible.</summary>
  private const decimal GapFactor = 1.03m;

  public static Candle[] Generate(AppIndicator indicator, TimeFrame timeFrame, int count)
  {
    var period = timeFrame.GetCandlePeriod();
    var end = FloorToPeriod(DateTime.UtcNow, period);
    var start = end - period * (count - 1);

    // Seed from the enum values (stable), not GetHashCode (identity-based).
    var rng = new Random(((int)indicator * 31 + (int)timeFrame) * 1009 + 17);

    var candles = new Candle[count];
    var price = 100m;
    var phase = (int)indicator;

    for (var i = 0; i < count; i++)
    {
      var step = 0.01 * Math.Sin(i / 12.0 + phase) + (rng.NextDouble() - 0.5) * 0.01;

      var open = price;
      var close = price * (1m + (decimal)step);
      var high = Math.Max(open, close) * (1m + (decimal)(rng.NextDouble() * 0.004));
      var low = Math.Min(open, close) * (1m - (decimal)(rng.NextDouble() * 0.004));
      var volume = 1000m + (decimal)(rng.NextDouble() * 2000);

      candles[i] = new Candle(
        Ticker,
        start + period * i,
        timeFrame,
        open,
        high,
        low,
        close,
        volume,
        isCompleted: true
      );

      price = close;
    }

    if (indicator == AppIndicator.Gap)
      InjectGap(candles);

    return candles;
  }

  /// <summary>
  /// Lifts one candle (and only it) above the previous close, so a visible gap opens
  /// around the two-thirds mark of the series. The following candles stay where they
  /// are, so the price may trade back into the gap afterwards.
  /// </summary>
  private static void InjectGap(Candle[] candles)
  {
    var index = candles.Length * 2 / 3;
    var candle = candles[index];

    candles[index] = new Candle(
      candle.Ticker,
      candle.Timestamp,
      candle.TimeFrame,
      candle.Open * GapFactor,
      candle.High * GapFactor,
      candle.Low * GapFactor,
      candle.Close * GapFactor,
      candle.Volume,
      candle.IsNotEmpty,
      candle.IsCompleted,
      candle.ExchangeName
    );
  }

  private static DateTime FloorToPeriod(DateTime utc, TimeSpan period)
  {
    if (period >= TimeSpan.FromDays(30))
      return new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    if (period >= TimeSpan.FromDays(7))
    {
      var daysSinceMonday = (int)(utc.DayOfWeek - DayOfWeek.Monday + 7) % 7;
      return new DateTime(utc.Year, utc.Month, utc.Day - daysSinceMonday, 0, 0, 0, DateTimeKind.Utc);
    }

    if (period >= TimeSpan.FromDays(1))
      return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);

    if (period >= TimeSpan.FromHours(1))
      return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, 0, DateTimeKind.Utc);

    return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, 0, DateTimeKind.Utc);
  }
}
