using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.StochRsi;

public class StochRsiIndicatorResult : IndicatorResult
{
  public double PercD { set; get; }
  public double PercDPrevious { get; set; }
  public double PercK { get; set; }
  public double PercKPrevious { get; set; }
  public double Delta { get; set; }
  public string Arrow { get; set; } = string.Empty;
  public StochRsiTrend Trend { get; set; }

  public static readonly double Epsilon = 0.0001;
  public static readonly string ArrowUp = "\u2197\uFE0F";
  public static readonly string ArrowDown = "\u2198\uFE0F";
  public static readonly string ArrowNeutral = "\u27A1\uFE0F";
}
