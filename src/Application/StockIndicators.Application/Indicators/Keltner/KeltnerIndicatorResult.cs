using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Keltner;

public class KeltnerIndicatorResult : IndicatorResult
{
  public double UpperBand { get; set; }
  public double MiddleBand { get; set; }
  public double LowerBand { get; set; }
  public ChannelPosition Position { get; set; }
}
