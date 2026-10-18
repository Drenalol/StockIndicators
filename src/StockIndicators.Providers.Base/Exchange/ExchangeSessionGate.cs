using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Providers.Base.Exchange;

/// <summary>
/// Maps a candle's exchange name onto its configured <see cref="ExchangeSession"/> and reports
/// whether the candle timestamp falls into an extended (pre-market, after-hours, overnight)
/// window. Only the minute timeframe is gated: extended hours arrive as separate minute bars,
/// while higher-timeframe candles already fold them into one bar. Exchanges without configured
/// extended windows (MOEX has none) and unknown exchanges (e.g. BINANCE, 24/7) are never extended.
/// </summary>
public sealed class ExchangeSessionGate : ITradingSessionGate
{
  // The US candle sources (Alpaca, BusinessQuant) track exactly these exchange names
  private static readonly HashSet<string> UsExchangeNames = new(StringComparer.OrdinalIgnoreCase)
  {
    "NASDAQ", "NYSE", "ARCA", "BATS", "OTC", "AMEX", "IEX", "NYSE MKT"
  };

  private static readonly HashSet<string> MoexExchangeNames = new(StringComparer.OrdinalIgnoreCase)
  {
    "MOEX"
  };

  private readonly ExchangeSession? _us;
  private readonly ExchangeSession? _moex;

  public ExchangeSessionGate(ExchangeSession? us, ExchangeSession? moex)
  {
    _us = us;
    _moex = moex;
  }

  public bool IsExtendedSession(string? exchangeName, TimeFrame timeFrame, DateTime candleUtc)
  {
    if (timeFrame != TimeFrame.Minute || exchangeName is not { } name)
      return false;

    var session = ResolveSession(exchangeName);

    if (session is null)
      return false;

    var utc = new DateTimeOffset(DateTime.SpecifyKind(candleUtc, DateTimeKind.Utc));

    return session.ExtendedKindAt(utc) is not null;
  }

  public MarketSession? CurrentMarketSession(string? exchangeName, DateTime nowUtc)
  {
    var session = ResolveSession(exchangeName);

    if (session is null)
      return null;

    var utc = Utc(nowUtc);

    return session.ExtendedKindAt(utc) switch
    {
      ExtendedSessionKind.Pre => MarketSession.PreMarket,
      ExtendedSessionKind.Post => MarketSession.AfterHours,
      ExtendedSessionKind.Overnight => MarketSession.Overnight,
      // known exchange, no extended window right now: either the regular session runs or
      // the exchange is fully closed (gap, weekend, holiday)
      _ => session.IsOpenAt(utc) ? MarketSession.Regular : MarketSession.Closed,
    };
  }

  /// <summary>
  /// Prices the newest minute candle as an extended quote when it falls into an extended
  /// window. The comparison is aligned on the session day (daily bars are keyed by the
  /// exchange's midnight): post-market extends its own day and compares to that day's
  /// daily bar; pre-market and the overnight session price the day that is about to open
  /// and compare to the last closed daily bar.
  /// </summary>
  public ExtendedQuoteInfo? ResolveExtendedQuote(string? exchangeName, DateTime minuteUtc, IReadOnlyList<Candle> dailyCandles)
  {
    var session = ResolveSession(exchangeName);

    if (session is null || !session.HasExtendedSessions)
      return null;

    var utc = new DateTimeOffset(DateTime.SpecifyKind(minuteUtc, DateTimeKind.Utc));

    if (session.ExtendedKindAt(utc) is not { } kind)
      return null;

    var sessionDay = SessionDayOfExtendedMinute(exchangeName, minuteUtc)!.Value;

    var reference = kind == ExtendedSessionKind.Post
      ? dailyCandles.LastOrDefault(candle => session.ToLocalDate(Utc(candle.Timestamp)) == sessionDay)
      : dailyCandles.LastOrDefault(candle => session.ToLocalDate(Utc(candle.Timestamp)) < sessionDay);

    return new ExtendedQuoteInfo(kind, reference is null ? null : (double)reference.Close);

    static DateTimeOffset Utc(DateTime timestamp) => new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
  }

  public DateOnly? CurrentExtendedDay(string? exchangeName, DateTime nowUtc)
  {
    var session = ResolveSession(exchangeName);

    if (session is null)
      return null;

    var utc = Utc(nowUtc);

    // pre/post extend the current local day's bar, the overnight session prices the day
    // that opens after it; the regular session is the day forming
    return session.ExtendedKindAt(utc) switch
    {
      ExtendedSessionKind.Pre or ExtendedSessionKind.Post => session.ToLocalDate(utc),
      ExtendedSessionKind.Overnight => session.OvernightSessionDayAt(utc),
      _ => session.IsOpenAt(utc) ? session.ToLocalDate(utc) : null,
    };
  }

  public ExtendedSessionKind? CurrentExtendedSession(string? exchangeName, DateTime nowUtc)
    => ResolveSession(exchangeName) is { } session ? session.ExtendedKindAt(Utc(nowUtc)) : null;

  public DateOnly? SessionDayOfExtendedMinute(string? exchangeName, DateTime minuteUtc)
  {
    var session = ResolveSession(exchangeName);

    if (session is null)
      return null;

    var utc = Utc(minuteUtc);

    return session.ExtendedKindAt(utc) switch
    {
      ExtendedSessionKind.Pre => session.ToLocalDate(utc),
      ExtendedSessionKind.Post => session.ToLocalDate(utc),
      ExtendedSessionKind.Overnight => session.OvernightSessionDayAt(utc),
      _ => null,
    };
  }

  public DateOnly? SessionDayOfMinute(string? exchangeName, DateTime minuteUtc)
  {
    var session = ResolveSession(exchangeName);

    if (session is null)
      return null;

    var utc = Utc(minuteUtc);

    return session.ExtendedKindAt(utc) switch
    {
      ExtendedSessionKind.Pre => session.ToLocalDate(utc),
      ExtendedSessionKind.Post => session.ToLocalDate(utc),
      ExtendedSessionKind.Overnight => session.OvernightSessionDayAt(utc),
      // a regular-session minute prices its own exchange-local day
      _ => session.IsOpenAt(utc) ? session.ToLocalDate(utc) : null,
    };
  }

  public DateTime? DailyAnchorUtc(string? exchangeName, DateOnly day)
    => ResolveSession(exchangeName) is { } session ? session.AnchorUtc(day).UtcDateTime : null;

  private static DateTimeOffset Utc(DateTime timestamp) => new(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

  private ExchangeSession? ResolveSession(string? exchangeName) =>
    exchangeName is { } name
      ? UsExchangeNames.Contains(name) ? _us : MoexExchangeNames.Contains(name) ? _moex : null
      : null;
}
