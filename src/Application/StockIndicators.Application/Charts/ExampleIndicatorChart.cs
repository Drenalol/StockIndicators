using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using AppIndicator = StockIndicators.Domain.Models.Indicators.Indicator;

namespace StockIndicators.Application.Charts;

/// <summary>
/// Builds an "example" chart for the indicator docs: the indicator overlay at sample
/// parameters on real historical candles (embedded AAPL/TSLA fixtures anchored on a candle
/// where the indicator triggers), through the same chart pipeline as trigger charts.
/// Falls back to the deterministic synthetic series only when no fixture is available.
/// </summary>
public static class ExampleIndicatorChart
{
  /// <summary>Minimum candle count for a readable chart.</summary>
  private const int MinCandleCount = 120;

  /// <summary>
  /// The example chart as raw series data (the payload the web client plots with its own
  /// chart core, the same one that draws the trigger and snapshot charts).
  /// </summary>
  public static ChartData RenderChartData(AppIndicator indicator, IIndicatorTemplate template, string? lang = null) =>
    ChartDataMapper.ToChartData(BuildSpec(indicator, template, lang));

  /// <summary>
  /// The example chart at the indicator's conventional sample parameters: instantiates the
  /// template, applies <see cref="ApplySampleDefaults"/> and renders. The one entry point for
  /// "the example chart of this indicator" (the docs endpoint and the link-preview card), so
  /// neither of them can forget the sample defaults and draw an empty chart.
  /// Throws what <c>GetTemplate</c> throws for an indicator without a template - callers
  /// decide how to degrade.
  /// </summary>
  public static ChartData RenderExampleChart(AppIndicator indicator, string? lang = null)
  {
    var template = (IIndicatorTemplate)Activator.CreateInstance(indicator.GetTemplate())!;

    ApplySampleDefaults(template);

    return RenderChartData(indicator, template, UserLocale.NormalizeLanguage(lang));
  }

  private static ChartSpec BuildSpec(AppIndicator indicator, IIndicatorTemplate template, string? lang)
  {
    var real = ExampleMarketData.TryGetWindow(indicator, out var example);

    var candles = real
      ? example!.Candles
      : ExampleCandles.Generate(indicator, ResolveFallbackTimeFrame(template), ResolveCandleCount(template));

    var built = IndicatorChartBuilder.Build(indicator, template, candles);
    var visible = TriggerChartService.TrimToIndicatorRange(candles, built.Series);

    return new ChartSpec(
      Title: BuildTitle(indicator, real ? example!.Ticker : null, real ? example!.TimeFrame : ResolveFallbackTimeFrame(template), lang),
      Candles: TriggerChartService.ToChartCandles(visible),
      Series: built.Series,
      HasOscillator: built.HasOscillator,
      OscMin: built.OscMin,
      OscMax: built.OscMax,
      TriggerAt: real ? example!.TriggerDate : null,
      TriggerLabel: real ? LocalizationCatalog.Get(LocalizationKey.ChartTrigger, lang) : null,
      // The example always ends on the trigger candle; the anchor's direction is the
      // handler's own comparison math replayed on the example series (guard-tested against
      // the production handler in ExampleMarketDataTests).
      TriggerDirection: real
        ? TriggerDirectionResolver.Resolve(indicator, template, candles, built.Series, example!.TriggerDate)
        : null);
  }

  internal static string BuildTitle(AppIndicator indicator, string? ticker, TimeFrame timeFrame, string? lang)
  {
    var example = LocalizationCatalog.Get(LocalizationKey.ChartExample, lang);
    var subject = ticker is null ? indicator.ToString() : $"{ticker} {indicator}";

    return $"{subject} {timeFrame} ({example})";
  }

  /// <summary>
  /// Same rule as the trigger chart: at least the readable window, but enough for the
  /// indicator to produce values across the whole chart (VWAP(390) needs 390 candles,
  /// or the series comes out entirely null).
  /// </summary>
  internal static int ResolveCandleCount(IIndicatorTemplate template)
  {
    if (template is ICandleCountDependentIndicatorTemplate candleCountDependent)
      return Math.Max(MinCandleCount, candleCountDependent.GetCandleCount());

    return MinCandleCount;
  }

  /// <summary>
  /// Timeframe of the synthetic fallback chart: the template's own timeframe where it has
  /// one, Day otherwise (the pre-fixture default). Real fixtures ignore this.
  /// </summary>
  private static TimeFrame ResolveFallbackTimeFrame(IIndicatorTemplate template) =>
    template is ITimeFrameDependentIndicatorTemplate timeFrameDependent ? timeFrameDependent.TimeFrame : TimeFrame.Day;

  /// <summary>
  /// Fills the zero-valued defaults with the conventional sample values so the example
  /// chart (and its candle count) is meaningful. Templates whose defaults are 0 are
  /// unusable as-is (SMA/EMA periods, StochRSI periods, Gap percent, RoundPrice accuracy).
  /// A live subscription may still carry the template's own default; the demo deviation
  /// only ever applies to the example gallery.
  /// </summary>
  public static void ApplySampleDefaults(IIndicatorTemplate template)
  {
    switch (template)
    {
      case MaIndicatorTemplate ma when ma.Periods < 1:
        ma.Periods = 20;
        break;

      case StochRsiIndicatorTemplate stochRsi when stochRsi.RsiPeriod < 1:
        stochRsi.RsiPeriod = 14;
        stochRsi.StochPeriod = 14;
        stochRsi.SignalPeriod = 3;
        stochRsi.SmoothPeriod = 3;
        // The 10% default tolerance draws a band wider than the picture explains; the
        // example shows the tighter, classic 5% one.
        stochRsi.Deviation = 0.05;
        break;

      case GapIndicatorTemplate gap when gap.Percent < 1:
        gap.Percent = 2;
        break;

      case RoundPriceIndicatorTemplate roundPrice when roundPrice.Accuracy < 1:
        roundPrice.Accuracy = 10;
        break;
    }
  }
}
