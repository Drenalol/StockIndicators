using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Rules;

/// <summary>Which extended window a candle belongs to.</summary>
public enum ExtendedSessionKind
{
  Pre = 0,
  Post = 1,
  /// <summary>The 20:00-04:00 overnight window (no Alpaca feed today; configurable for the future).</summary>
  Overnight = 2,
}

/// <summary>
/// The market phase of an exchange at a moment: the regular session, one of the extended
/// windows, or fully closed (gap between sessions, weekend, holiday). Exchanges the gate
/// does not model (24/7 crypto venues) have no phase at all.
/// </summary>
public enum MarketSession
{
  Regular = 0,
  PreMarket = 1,
  AfterHours = 2,
  Overnight = 3,
  Closed = 4,
}

/// <summary>
/// The extended quote of an instrument: the latest extended-hours trade and the regular
/// session close it should be compared against (the day's own daily bar for post-market,
/// the last closed daily bar for pre-market and for the overnight session; null when no
/// reference bar exists yet).
/// </summary>
public readonly record struct ExtendedQuoteInfo(ExtendedSessionKind Kind, double? ReferenceClose);

/// <summary>
/// Tells whether a freshly arrived candle belongs to an extended trading session
/// (pre/post-market) of its exchange. By default rule conditions evaluated on such candles
/// are silently unmet (the per-subscription <c>IgnoreExtendedSessions</c> opt-out):
/// extended-hours minute bars are real market data (they keep flowing into the candle
/// cache and the charts), but alerts on them surprise users who think in regular-session
/// terms. Exchanges without configured pre/post windows (and instruments of unknown
/// exchanges) are never considered extended.
/// </summary>
public interface ITradingSessionGate
{
  bool IsExtendedSession(string? exchangeName, TimeFrame timeFrame, DateTime candleUtc);

  /// <summary>
  /// The exchange's market phase at the moment (for the "exchange closed" chart status).
  /// Null when the gate does not model the exchange at all (24/7 crypto venues, unknown
  /// exchanges): the caller must show no session status rather than a bogus "closed".
  /// </summary>
  MarketSession? CurrentMarketSession(string? exchangeName, DateTime nowUtc);

  /// <summary>
  /// Resolves the extended quote of an instrument from its newest minute candle: the kind of
  /// extended window the minute falls into, and the daily bar it should be priced against
  /// (post-market compares to its own day's bar, pre-market to the last closed bar). Null
  /// when the minute is a regular-session candle or the exchange has no extended windows.
  /// </summary>
  ExtendedQuoteInfo? ResolveExtendedQuote(string? exchangeName, DateTime minuteUtc, IReadOnlyList<Candle> dailyCandles);

  /// <summary>
  /// The trading day whose daily candle is forming right now under an extended (pre/post)
  /// or regular session on this exchange; null in the overnight gap and on weekends.
  /// </summary>
  DateOnly? CurrentExtendedDay(string? exchangeName, DateTime nowUtc);

  /// <summary>The extended window open at the moment (null during regular sessions and gaps).</summary>
  ExtendedSessionKind? CurrentExtendedSession(string? exchangeName, DateTime nowUtc);

  /// <summary>
  /// The trading day an extended (pre/post/overnight) minute candle contributes to on the
  /// daily chart (its exchange-local calendar day; an overnight minute would belong to the
  /// next trading day). Null for regular-session minutes and unknown exchanges.
  /// </summary>
  DateOnly? SessionDayOfExtendedMinute(string? exchangeName, DateTime minuteUtc);

  /// <summary>
  /// The trading day a minute candle contributes to on the daily chart, whatever session it
  /// comes from: pre/post and regular minutes price their exchange-local day, overnight
  /// minutes the day that opens after them. Null for minutes outside any session of the
  /// exchange (and for exchanges the gate does not know).
  /// </summary>
  DateOnly? SessionDayOfMinute(string? exchangeName, DateTime minuteUtc);

  /// <summary>The UTC timestamp a daily candle of the trading day is anchored at (the exchange's midnight).</summary>
  DateTime? DailyAnchorUtc(string? exchangeName, DateOnly day);
}
