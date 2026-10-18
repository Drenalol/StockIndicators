using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Donchian;

public class DonchianIndicatorResult : IndicatorResult
{
  public double UpperBand { get; set; }
  public double MiddleBand { get; set; }
  public double LowerBand { get; set; }
  public ChannelPosition Position { get; set; }
}
