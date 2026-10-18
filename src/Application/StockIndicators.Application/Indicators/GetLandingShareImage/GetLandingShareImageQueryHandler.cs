using LazyCache;
using Microsoft.Extensions.Options;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.GetLandingShareImage;

public class GetLandingShareImageQueryHandler : IQueryHandler<GetLandingShareImageQuery, byte[]?>
{
  /// <summary>
  /// The brand line of the card. Not localized: a brand name reads as itself in every
  /// language, and the string the messengers already see in <c>og:site_name</c>.
  /// </summary>
  private const string BrandTitle = "Stock Indicators";

  /// <summary>
  /// A handful of pictures in total (one per language), each deterministic per app version;
  /// kept as long as the indicator cards so a landing link and an indicator link cannot
  /// start rendering with a different browser budget.
  /// </summary>
  private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

  private readonly IAppCache _cache;
  private readonly IChartImageRenderer _renderer;
  private readonly ShareImageOptions _options;
  private readonly ILogger _logger;
  private bool _configuredWarned;

  public GetLandingShareImageQueryHandler(
    IAppCache cache,
    IChartImageRenderer renderer,
    IOptions<ShareImageOptions> options,
    ILogger logger)
  {
    _cache = cache;
    _renderer = renderer;
    _options = options.Value;
    _logger = logger.ForContext<GetLandingShareImageQueryHandler>();
  }

  public async Task<byte[]?> Handle(GetLandingShareImageQuery request, CancellationToken cancellationToken)
  {
    var language = UserLocale.NormalizeLanguage(request.Language);

    if (!TryGetIndicator(out var indicator))
      return null;

    var key = GetLandingShareImageQuery.CacheKey(indicator.ToString(), language);

    if (_cache.TryGetValue<byte[]>(key, out var cached) && cached is { Length: > 0 })
      return cached;

    var png = await RenderAsync(indicator, language, cancellationToken);

    if (png is { Length: > 0 })
      _cache.Add(key, png, DateTimeOffset.UtcNow.Add(CacheTtl));

    return png;
  }

  /// <summary>
  /// The deployment's choice, parsed once a request touches it: a typo in
  /// <c>ShareImage:LandingIndicator</c> answers the brand fallback rather than a picture of
  /// some other indicator, and says so once in the log.
  /// </summary>
  private bool TryGetIndicator(out Indicator indicator)
  {
    if (Enum.TryParse<Indicator>(_options.LandingIndicator, ignoreCase: true, out indicator))
      return true;

    if (!_configuredWarned)
    {
      _configuredWarned = true;
      _logger.Warning(
        "Share image: ShareImage:LandingIndicator is {Value}, which is not an indicator; the landing page keeps its brand fallback picture",
        _options.LandingIndicator);
    }

    return false;
  }

  private async Task<byte[]?> RenderAsync(Indicator indicator, string language, CancellationToken cancellationToken)
  {
    ChartData chart;

    try
    {
      // the anchored example fixture: real candles, the indicator's sample parameters, and
      // a marker on the candle the production handler triggers on
      chart = ExampleIndicatorChart.RenderExampleChart(indicator, language);
    }
    catch (Exception ex)
    {
      if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        throw;

      _logger.Warning(ex, "Share image: the landing example chart of {Indicator} could not be built", indicator);

      return null;
    }

    try
    {
      return await _renderer.RenderAsync(
        chart,
        new UserLocale(language, TimeZoneInfo.Utc),
        ChartRenderOptions.Card(
          BrandTitle,
          // the same one-line pitch the bot's profile carries: a preview card in Telegram is
          // what both are for, so the wording is not maintained twice
          LocalizationCatalog.Get(LocalizationKey.BotProfileShortDescription, language),
          eyebrow: null),
        cancellationToken);
    }
    catch (Exception ex)
    {
      if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        throw;

      _logger.Warning(ex, "Share image: rendering the landing picture of {Indicator} failed", indicator);

      return null;
    }
  }
}
