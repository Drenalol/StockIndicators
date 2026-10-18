using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Resolves the extended-hours context of a daily chart in one pass: fetches the recent
/// minute candles once, prices the newest of them as the pre/post/overnight quote line
/// (with a localized label for the chart's price-scale tag), and merges the extended
/// minutes into the daily series so the chart shows the live forming bar (the exchange's
/// own daily bars never carry extended prices). Every miss (no gate, cold minute cache,
/// regular-session candle, cancelled fetch) simply means "no extended data": a chart
/// request must never fail because of the extended feature.
/// </summary>
public static class ExtendedQuoteResolver
{
  /// <summary>Minute candles considered: a full US extended day is pre(330)+post(240) plus
  /// the overnight session (480) with slack; the fetch reads the L1 minute cache anyway.</summary>
  public const int MinuteWindow = 900;

  /// <summary>Hard cap for the minute fetch (a cold cache backfills from the provider).</summary>
  private static readonly TimeSpan MinuteFetchTimeout = TimeSpan.FromSeconds(20);

  public sealed record Resolution(ExtendedQuoteData? Quote, Candle[] Candles);

  public static async Task<Resolution> ResolveAsync(
    ICandleRepository candleRepository,
    ITradingSessionGate? tradingSessionGate,
    string ticker,
    string? exchangeName,
    IReadOnlyList<Candle> dailyCandles,
    UserLocale locale,
    CancellationToken cancellationToken = default)
  {
    if (tradingSessionGate is null)
      return new Resolution(null, ToArray(dailyCandles));

    Candle[] minutes;

    using var minuteFetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    minuteFetchCts.CancelAfter(MinuteFetchTimeout);

    try
    {
      minutes = await candleRepository
        .GetLastCandlesAsync(ticker, exchangeName, MinuteWindow, TimeFrame.Minute, minuteFetchCts.Token);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      return new Resolution(null, ToArray(dailyCandles));
    }

    ExtendedQuoteData? quote = null;

    if (minutes.Length > 0)
    {
      var lastMinute = minutes[^1];

      if (tradingSessionGate.ResolveExtendedQuote(exchangeName, lastMinute.Timestamp, dailyCandles) is { } info)
      {
        var session = info.Kind switch
        {
          ExtendedSessionKind.Pre => "pre",
          ExtendedSessionKind.Post => "post",
          ExtendedSessionKind.Overnight => "overnight",
          _ => null,
        };

        var labelKey = info.Kind switch
        {
          ExtendedSessionKind.Pre => (LocalizationKey?) LocalizationKey.ChartPreMarket,
          ExtendedSessionKind.Post => LocalizationKey.ChartAfterHours,
          ExtendedSessionKind.Overnight => LocalizationKey.ChartOvernight,
          _ => null,
        };

        if (session is not null && labelKey is not null)
          quote = new ExtendedQuoteData(
            session,
            (double)lastMinute.Close,
            DateTime.SpecifyKind(lastMinute.Timestamp, DateTimeKind.Utc),
            info.ReferenceClose,
            LocalizationCatalog.Get(labelKey.Value, locale.Language));
      }
    }

    return new Resolution(quote, ExtendedChartComposer.Merge(dailyCandles, minutes, tradingSessionGate, DateTime.UtcNow));
  }

  private static Candle[] ToArray(IReadOnlyList<Candle> candles) => candles as Candle[] ?? candles.ToArray();
}
