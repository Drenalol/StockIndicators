using StockIndicators.Application.Rules;

namespace StockIndicators.Providers.Base.Exchange;

public sealed class ExchangeSession
{
  // The overnight (Blue Ocean style) session runs on the evenings before trading days,
  // so a US calendar week has five of them: Sunday through Thursday evenings (Friday and
  // Saturday nights are dark). The window itself is configured per exchange.
  private static readonly ISet<DayOfWeek> OvernightStartDays =
    new HashSet<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday };

  private readonly TimeZoneInfo _timeZone;
  private readonly IReadOnlyList<ExchangeSessionWindow> _sessions;
  private readonly ExchangeSessionWindow? _preMarket;
  private readonly ExchangeSessionWindow? _postMarket;
  private readonly ExchangeSessionWindow? _overnight;
  private readonly ISet<DayOfWeek> _weekendDays;
  private readonly ISet<DateOnly> _holidays;
  private readonly TimeProvider _timeProvider;

  public ExchangeSession(ExchangeSessionOptions options, TimeProvider? timeProvider = null)
  {
    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    _sessions = options.Sessions.OrderBy(window => window.Open).ToArray();
    _preMarket = options.PreMarket;
    _postMarket = options.PostMarket;
    _overnight = options.Overnight;
    _weekendDays = options.WeekendDays.ToHashSet();
    _holidays = options.Holidays.ToHashSet();
    _timeProvider = timeProvider ?? TimeProvider.System;
  }

  public string TimeZoneId => _timeZone.Id;

  public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

  public bool IsTradingDay(DateOnly localDate)
    => !_weekendDays.Contains(localDate.DayOfWeek) && !_holidays.Contains(localDate);

  public bool IsOpenAt(DateTimeOffset utc)
  {
    var local = ToLocalWallTime(utc);
    return _sessions.Any(window => IsWithinWindow(local, window));
  }

  public bool IsPreMarketAt(DateTimeOffset utc)
    => _preMarket is { } window && IsWithinWindow(ToLocalWallTime(utc), window);

  public bool IsPostMarketAt(DateTimeOffset utc)
    => _postMarket is { } window && IsWithinWindow(ToLocalWallTime(utc), window);

  /// <summary>True when the exchange has any extended (pre/post/overnight) session windows configured.</summary>
  public bool HasExtendedSessions => _preMarket is not null || _postMarket is not null || _overnight is not null;

  /// <summary>Which extended window the moment falls into (null for regular sessions and gaps).</summary>
  public ExtendedSessionKind? ExtendedKindAt(DateTimeOffset utc)
  {
    var local = ToLocalWallTime(utc);

    if (_preMarket is { } pre && IsWithinWindow(local, pre))
      return ExtendedSessionKind.Pre;

    if (_postMarket is { } post && IsWithinWindow(local, post))
      return ExtendedSessionKind.Post;

    if (OvernightSessionDay(local) is not null)
      return ExtendedSessionKind.Overnight;

    return null;
  }

  /// <summary>The exchange-local calendar date of a UTC moment (daily-bar alignment for extended quotes).</summary>
  public DateOnly ToLocalDate(DateTimeOffset utc) => DateOnly.FromDateTime(ToLocalWallTime(utc));

  /// <summary>
  /// The trading day an overnight-session moment prices: the evening of a Sunday-Thursday
  /// opens the next trading day's session, and its early-morning tail belongs to the day
  /// whose evening started it. Null in Friday/Saturday nights and outside the window.
  /// </summary>
  public DateOnly? OvernightSessionDay(DateTime local)
  {
    if (_overnight is not { } window)
      return null;

    var date = DateOnly.FromDateTime(local.Date);

    if (local.TimeOfDay >= window.Open)
      return OvernightStartDays.Contains(local.DayOfWeek) ? NextTradingDay(date) : null;

    if (local.TimeOfDay < window.Close)
    {
      var evening = date.AddDays(-1);

      return OvernightStartDays.Contains(evening.DayOfWeek) && NextTradingDay(evening) == date ? date : null;
    }

    return null;
  }

  /// <summary>The trading day an overnight-session UTC moment prices (see the local-time overload).</summary>
  public DateOnly? OvernightSessionDayAt(DateTimeOffset utc) => OvernightSessionDay(ToLocalWallTime(utc));

  /// <summary>The UTC anchor of a trading day's candle (the exchange's own midnight, the daily-bar timestamp convention).</summary>
  public DateTimeOffset AnchorUtc(DateOnly localDate) => ToUtc(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

  /// <summary>The first trading day strictly after the given local date (null beyond a week: never in practice).</summary>
  public DateOnly? NextTradingDay(DateOnly localDate)
  {
    for (var offset = 1; offset <= 7; offset++)
    {
      var date = localDate.AddDays(offset);

      if (IsTradingDay(date))
        return date;
    }

    return null;
  }

  public DateTimeOffset GetNextOpen(DateTimeOffset utc)
  {
    var local = ToLocalWallTime(utc);

    for (var offset = 0; offset < 31; offset++)
    {
      var date = local.Date.AddDays(offset);

      if (!IsTradingDay(DateOnly.FromDateTime(date)))
        continue;

      foreach (var window in _sessions)
      {
        var openUtc = ToUtc(date, window.Open);

        if (openUtc > utc)
          return openUtc;
      }
    }

    throw new InvalidOperationException($"No {TimeZoneId} session start found within 31 days after {utc:O}");
  }

  /// <summary>
  /// The next moment any window of the exchange opens: regular, pre, post or overnight.
  /// Used by the poller gates that also run during extended hours (the extended minutes
  /// are real bars, only their alerts are gated). Overnight opens price the next trading
  /// day, so their evenings are not trading days themselves (Sunday evening included).
  /// </summary>
  public DateTimeOffset GetNextExtendedOpen(DateTimeOffset utc)
  {
    var local = ToLocalWallTime(utc);

    DateTimeOffset? next = null;

    void Consider(DateTimeOffset candidate)
    {
      if (candidate > utc && (next is null || candidate < next.Value))
        next = candidate;
    }

    for (var offset = 0; offset < 31; offset++)
    {
      var date = local.Date.AddDays(offset);
      var day = DateOnly.FromDateTime(date);

      if (IsTradingDay(day))
      {
        foreach (var window in _sessions)
          Consider(ToUtc(date, window.Open));

        if (_preMarket is { } pre)
          Consider(ToUtc(date, pre.Open));

        if (_postMarket is { } post)
          Consider(ToUtc(date, post.Open));
      }

      if (_overnight is { } overnight && OvernightStartDays.Contains(date.DayOfWeek) && NextTradingDay(day) is not null)
        Consider(ToUtc(date, overnight.Open));

      if (next is not null)
        return next.Value;
    }

    throw new InvalidOperationException($"No extended {TimeZoneId} session start found within 31 days after {utc:O}");
  }

  private bool IsWithinWindow(DateTime local, ExchangeSessionWindow window)
  {
    var date = DateOnly.FromDateTime(local.Date);

    if (window.Open <= window.Close)
      return local.TimeOfDay >= window.Open
        && local.TimeOfDay < window.Close
        && IsTradingDay(date);

    if (local.TimeOfDay >= window.Open)
      return IsTradingDay(date);

    return local.TimeOfDay < window.Close
      && IsTradingDay(date.AddDays(-1));
  }

  private DateTime ToLocalWallTime(DateTimeOffset utc)
  {
    var local = TimeZoneInfo.ConvertTime(utc, _timeZone);
    return new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, DateTimeKind.Unspecified);
  }

  private DateTimeOffset ToUtc(DateTime date, TimeSpan timeOfDay)
  {
    var local = DateTime.SpecifyKind(date.Date.Add(timeOfDay), DateTimeKind.Unspecified);
    return new DateTimeOffset(local, _timeZone.GetUtcOffset(local));
  }
}
