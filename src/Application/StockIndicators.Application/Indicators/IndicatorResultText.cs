using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Indicators.PriceChange;
using StockIndicators.Application.Indicators.OpenChange;
using StockIndicators.Application.Indicators.StochRsi;
using StockIndicators.Application.Indicators.TrailingStop;
using StockIndicators.Application.Indicators.Volume;
using StockIndicators.Application.Indicators.Week52HighLow;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators;

/// <summary>
/// Renders an <see cref="IndicatorResult"/> as a short human-readable value, shared by the flat
/// subscription pipeline and the rule leaf evaluator so notifications stay consistent.
/// Words are localized to the user's language; the "prev" gap date is drawn in the
/// user's timezone.
/// </summary>
public static class IndicatorResultText
{
  // The indicator name is already shown by the alert line (rule condition prefix or the channel
  // line prefix), so the value text must not repeat it.
  public static string Build(IndicatorResult indicatorResult, UserLocale locale) =>
    indicatorResult switch
    {
      GapIndicatorResult gap =>
        $"{AlertTexts.FormatNumber(gap.Percent, "F2")}% " +
        $"({AlertTexts.GapPreviousLabel(locale.Language)}: {AlertTexts.FormatDate(gap.Previous, locale.Timezone, "yyyy-MM-dd")})",
      MaIndicatorResult ma =>
        $"{AlertTexts.FormatNumber(ma.Result, "F4")} ({AlertTexts.MaTestLabel(locale.Language, ma.Test)})",
      StochRsiIndicatorResult stochRsi =>
        $"{stochRsi.Arrow} %K: {AlertTexts.FormatNumber(stochRsi.PercK, "F2")}, " +
        $"%D: {AlertTexts.FormatNumber(stochRsi.PercD, "F2")}, Δ: {AlertTexts.FormatNumber(stochRsi.Delta, "F4")}",
      VolumeIndicatorResult volume =>
        $"{AlertTexts.FormatNumber(volume.Ratio, "F2")}× {AlertTexts.VolumeAverageLabel(locale.Language)}",
      PriceChangeIndicatorResult change =>
        $"{AlertTexts.FormatNumber(change.Change, "+0.00;-0.00;0.00")}%",
      OpenChangeIndicatorResult openChange =>
        $"{AlertTexts.FormatNumber(openChange.Change, "+0.00;-0.00;0.00")}% {AlertTexts.OpenChangeLabel(locale.Language)}",
      Week52HighLowIndicatorResult week52 =>
        $"{AlertTexts.FormatNumber(week52.Value, "+0.00;-0.00;0.00")}% {AlertTexts.Week52Label(locale.Language, week52.Kind)}",
      TrailingStopIndicatorResult trailing =>
        $"{AlertTexts.FormatNumber(trailing.Change, "+0.00;-0.00;0.00")}% {AlertTexts.TrailingAnchorLabel(locale.Language, trailing.LongPosition)}",
      _ => AlertTexts.Triggered(locale.Language)
    };
}
