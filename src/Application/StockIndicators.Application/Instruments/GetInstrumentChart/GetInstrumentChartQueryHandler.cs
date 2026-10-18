using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Instruments.GetInstrumentChart;

/// <summary>
/// The plain price preview chart of an instrument: the newest daily candles without any
/// indicator overlay or trigger marker, re-stamped to the viewer's timezone. Same candle
/// pipeline and <see cref="ChartData"/> payload as the subscription charts, so the client
/// draws it with its own chart core. Extended sessions are folded into the daily bar the
/// way the quote services show them (TradingView-style): the chart carries the live
/// forming bar of the day during pre-market, after-hours and overnight, with a labeled
/// quote line for the price-scale tag.
/// </summary>
public class GetInstrumentChartQueryHandler : IQueryHandler<GetInstrumentChartQuery, ChartData?>
{
  /// <summary>Readable candle window of the plain price preview: roughly half a year of daily candles.</summary>
  private const int CandleWindow = 130;

  /// <summary>Hard cap for the candle fetch (a cold instrument backfills from the provider):
  /// the same ceiling as the snapshot chart, so a stalled source never hangs the request.</summary>
  private static readonly TimeSpan CandleFetchTimeout = TimeSpan.FromSeconds(20);

  private readonly ICandleRepository _candleRepository;
  private readonly IUserLocaleProvider _localeProvider;
  private readonly ITradingSessionGate? _tradingSessionGate;
  private readonly ILogger _logger;

  public GetInstrumentChartQueryHandler(
    ICandleRepository candleRepository,
    IUserLocaleProvider localeProvider,
    ILogger logger,
    ITradingSessionGate? tradingSessionGate = null
  )
  {
    _candleRepository = candleRepository;
    _localeProvider = localeProvider;
    _tradingSessionGate = tradingSessionGate;
    _logger = logger.ForContext<GetInstrumentChartQueryHandler>();
  }

  public async Task<ChartData?> Handle(GetInstrumentChartQuery request, CancellationToken cancellationToken)
  {
    var locale = await _localeProvider.GetAsync(request.UserId, cancellationToken);

    using var candleFetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    candleFetchCts.CancelAfter(CandleFetchTimeout);

    Candle[] candles;

    try
    {
      candles = await _candleRepository
        .GetLastCandlesAsync(request.Ticker, request.ExchangeName, CandleWindow, TimeFrame.Day, candleFetchCts.Token);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      _logger.Warning(
        "Instrument chart candle fetch for {Ticker} ({ExchangeName}) timed out after {TimeoutSeconds}s",
        request.Ticker,
        request.ExchangeName,
        CandleFetchTimeout.TotalSeconds
      );

      return null;
    }

    if (candles.Length < 2)
    {
      _logger.Information(
        "Instrument chart unavailable for {Ticker} ({ExchangeName}): not enough candles in cache ({Count})",
        request.Ticker,
        request.ExchangeName,
        candles.Length
      );

      return null;
    }

    // Extended sessions ride along with the daily series: the composer folds extended
    // minutes into the forming day's bar and prices the newest of them for the quote line
    var resolution = await ExtendedQuoteResolver.ResolveAsync(
      _candleRepository, _tradingSessionGate, request.Ticker, request.ExchangeName, candles, locale, cancellationToken);

    // Display-only re-stamp to the viewer's wall clock, exactly like the snapshot chart
    var displayCandles = TriggerChartService.ToDisplayTimezone(resolution.Candles, locale.Timezone);

    var spec = new ChartSpec(
      Title: TriggerChartService.BuildSnapshotTitle(request.Ticker, TimeFrame.Day, locale, DateTime.UtcNow),
      Candles: TriggerChartService.ToChartCandles(displayCandles),
      Series: Array.Empty<ChartSeries>(),
      HasOscillator: false,
      OscMin: null,
      OscMax: null,
      // The freshness anchor stays UTC (the raw fetched candle, not the display re-stamp)
      LastCandleAt: DateTime.SpecifyKind(resolution.Candles[^1].Timestamp, DateTimeKind.Utc),
      Extended: resolution.Quote,
      // The exchange phase behind the "exchange closed" status of the chart header
      Session: _tradingSessionGate?.CurrentMarketSession(
        request.ExchangeName ?? resolution.Candles[^1].ExchangeName, DateTime.UtcNow)
    );

    return ChartDataMapper.ToChartData(spec);
  }
}
