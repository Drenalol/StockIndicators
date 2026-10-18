using LazyCache;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.GetLandingExamples;

public class GetLandingExamplesQueryHandler : IQueryHandler<GetLandingExamplesQuery, LandingExampleChartDto[]>
{
  /// <summary>
  /// Curated slides: diverse indicator families and chart shapes (price overlays, oscillators,
  /// histograms), each with a real AAPL/TSLA fixture window ending on a candle where the
  /// indicator fires at the example parameters, so every slide shows a genuine trigger moment.
  /// </summary>
  private static readonly Indicator[] Gallery =
  [
    Indicator.SMA,
    Indicator.RSI,
    Indicator.MACD,
    Indicator.BollingerBands,
    Indicator.SuperTrend,
    Indicator.StochRSI,
    Indicator.Ichimoku,
    Indicator.Gap,
    Indicator.PriceChange,
  ];

  /// <summary>The gallery is rendered from embedded fixtures - deterministic per language, so a long TTL is safe.</summary>
  private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

  private readonly IAppCache _cache;

  public GetLandingExamplesQueryHandler(IAppCache cache)
  {
    _cache = cache;
  }

  public Task<LandingExampleChartDto[]> Handle(GetLandingExamplesQuery request, CancellationToken cancellationToken) =>
    _cache.GetOrAddAsync(
      $"landing-examples:{request.Language}",
      () => RenderAllAsync(request.Language),
      DateTimeOffset.UtcNow.Add(CacheTtl));

  private static Task<LandingExampleChartDto[]> RenderAllAsync(string language)
  {
    var slides = new List<LandingExampleChartDto>(Gallery.Length);

    foreach (var indicator in Gallery)
    {
      // The gallery promises real candles: without a fixture the example pipeline would draw
      // the synthetic fallback chart, so such an indicator is skipped instead
      if (!ExampleMarketData.TryGetWindow(indicator, out var window))
        continue;

      var template = (IIndicatorTemplate)Activator.CreateInstance(indicator.GetTemplate())!;
      ExampleIndicatorChart.ApplySampleDefaults(template);

      var chart = ExampleIndicatorChart.RenderChartData(indicator, template, language);

      slides.Add(new LandingExampleChartDto(
        indicator.ToString(),
        window.Ticker,
        window.Candles.Length > 0 ? window.Candles[^1].ExchangeName : null,
        chart));
    }

    return Task.FromResult(slides.ToArray());
  }
}
