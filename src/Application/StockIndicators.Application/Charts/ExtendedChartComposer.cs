using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Builds the TradingView-style "extended hours in the daily bar": pre-market, after-hours
/// (and overnight when a feed provides it) minute candles are merged into the daily candle
/// they price, because the exchange's own daily bars never carry extended prices (the trade
/// conditions of the consolidated tape only let extended trades move the day's volume).
/// The chart therefore shows the live forming bar of the current day while an extended
/// session runs (a synthetic bar anchored at the exchange's midnight before the regular
/// session has produced the first trade), instead of stopping at yesterday's last close.
/// </summary>
public static class ExtendedChartComposer
{
  /// <summary>
  /// Merges the day's session minute candles into the daily series. Pure: returns the input
  /// array unchanged when there is nothing to extend (no gate, no minutes for the day being
  /// formed, weekend gap). Only the last daily candle can be touched or appended.
  /// </summary>
  public static Candle[] Merge(IReadOnlyList<Candle> dailyCandles, IReadOnlyList<Candle> minuteCandles, ITradingSessionGate? tradingSessionGate, DateTime nowUtc)
  {
    var dailies = dailyCandles as Candle[] ?? dailyCandles.ToArray();

    if (tradingSessionGate is null || minuteCandles.Count == 0)
      return dailies;

    var exchangeName = dailies.Length > 0
      ? dailies[^1].ExchangeName
      : minuteCandles[^1].ExchangeName;

    // The day whose bar is being formed right now (null outside sessions and on weekends)
    if (tradingSessionGate.CurrentExtendedDay(exchangeName, nowUtc) is not { } day)
      return dailies;

    var sessionMinutes = minuteCandles
      .Where(candle => candle.IsNotEmpty && tradingSessionGate.SessionDayOfMinute(candle.ExchangeName, candle.Timestamp) == day)
      .OrderBy(candle => candle.Timestamp)
      .ToArray();

    if (sessionMinutes.Length == 0)
      return dailies;

    var anchor = tradingSessionGate.DailyAnchorUtc(exchangeName, day) ?? sessionMinutes[^1].Timestamp;

    var existing = dailies.Length > 0 && DateTime.SpecifyKind(dailies[^1].Timestamp, DateTimeKind.Utc) == anchor
      ? dailies[^1]
      : null;

    // The exchange's own bar for the day has not materialized yet (Alpaca serves completed
    // daily bars only, a while after the close): the session minute candles still in the
    // cache form the day's live candle - the same "forming bar" the quote services draw.
    // It starts at the day's first trade (overnight first, then pre-market, then the regular
    // open), the newest minute is its live price, the volume sums every session.
    if (existing is null)
    {
      var synthetic = new Candle(
        sessionMinutes[^1].Ticker,
        anchor,
        TimeFrame.Day,
        sessionMinutes[0].Open,
        sessionMinutes.Max(candle => candle.High),
        sessionMinutes.Min(candle => candle.Low),
        sessionMinutes[^1].Close,
        sessionMinutes.Sum(candle => candle.Volume),
        isNotEmpty: true,
        isCompleted: false,
        exchangeName: exchangeName);

      return [.. dailies, synthetic];
    }

    // The exchange's own bar exists: regular-session minutes are already inside it, so only
    // the extended minutes merge in (the consolidated tape never let them move its prices).
    // The volume is the bar's own: the tape did let extended trades move it, so adding the
    // extended minutes' volume again would count them twice.
    var extendedMinutes = sessionMinutes
      .Where(candle => tradingSessionGate.SessionDayOfExtendedMinute(candle.ExchangeName, candle.Timestamp) == day)
      .ToArray();

    if (extendedMinutes.Length == 0)
      return dailies;

    var merged = new Candle(
      existing.Ticker,
      anchor,
      TimeFrame.Day,
      extendedMinutes[0].Open,
      Math.Max(existing.High, extendedMinutes.Max(candle => candle.High)),
      Math.Min(existing.Low, extendedMinutes.Min(candle => candle.Low)),
      extendedMinutes[^1].Close,
      existing.Volume,
      isNotEmpty: true,
      isCompleted: false,
      exchangeName: exchangeName);

    return [.. dailies[..^1], merged];
  }
}
