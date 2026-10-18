using System.Reflection;
using LazyCache;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Application.Indicators.GetIndicatorShareImage;

public class GetIndicatorShareImageQueryHandler : IQueryHandler<GetIndicatorShareImageQuery, byte[]?>
{
  /// <summary>
  /// The picture is deterministic per app version (fixture candles, sample parameters),
  /// and the set of cached pictures is bounded by indicators x languages, so a day in
  /// process is enough to survive the burst of unfurls a shared link makes while
  /// messengers and Cloudflare cache the response for far longer.
  /// </summary>
  private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

  private readonly IAppCache _cache;
  private readonly IChartImageRenderer _renderer;
  private readonly ILogger _logger;

  public GetIndicatorShareImageQueryHandler(IAppCache cache, IChartImageRenderer renderer, ILogger logger)
  {
    _cache = cache;
    _renderer = renderer;
    _logger = logger.ForContext<GetIndicatorShareImageQueryHandler>();
  }

  public async Task<byte[]?> Handle(GetIndicatorShareImageQuery request, CancellationToken cancellationToken)
  {
    // Normalized here as well as at the endpoint: this is an anonymous URL a crawler hits
    // with any ?lang=, and the language goes on to open a browser context, which rejects a
    // language tag it cannot parse. It also keeps "zz" and "en" one cache entry.
    var language = UserLocale.NormalizeLanguage(request.Language);
    var key = GetIndicatorShareImageQuery.CacheKey(request.Indicator, language);

    if (_cache.TryGetValue<byte[]>(key, out var cached) && cached is { Length: > 0 })
      return cached;

    var png = await RenderAsync(request.Indicator, language, cancellationToken);

    // A failed render is not cached: the next unfurl tries again (and the caller answers
    // with the static brand card until the browser is up), rather than serving the fallback
    // for a day after one-off failure
    if (png is { Length: > 0 })
      _cache.Add(key, png, DateTimeOffset.UtcNow.Add(CacheTtl));

    return png;
  }

  private async Task<byte[]?> RenderAsync(Indicator indicator, string language, CancellationToken cancellationToken)
  {
    ChartData chart;
    string? description;

    try
    {
      chart = ExampleIndicatorChart.RenderExampleChart(indicator, language);
      description = indicator.GetTemplate().GetCustomAttribute<IndicatorHintAttribute>()?.GetText(language);
    }
    catch (Exception ex)
    {
      // a crawler that hung up mid-render is not a failure to log about
      if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        throw;

      // an indicator without a template, or an example that cannot be computed, has no card
      _logger.Warning(ex, "Indicator share image: the example chart of {Indicator} could not be built", indicator);

      return null;
    }

    try
    {
      // The card is the app's own chart core again (the alert image pipeline): the language
      // localizes the axis labels and the OHLC row, which is pinned on the trigger candle
      return await _renderer.RenderAsync(
        chart,
        new UserLocale(language, TimeZoneInfo.Utc),
        ChartRenderOptions.Card(indicator.ToString(), description),
        cancellationToken);
    }
    catch (Exception ex)
    {
      if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        throw;

      _logger.Warning(ex, "Indicator share image: rendering {Indicator} failed", indicator);

      return null;
    }
  }
}
