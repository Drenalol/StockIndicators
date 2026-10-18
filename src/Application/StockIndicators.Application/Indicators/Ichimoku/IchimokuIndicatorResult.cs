using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Ichimoku;

public class IchimokuIndicatorResult : IndicatorResult
{
  public double TenkanSen { get; set; }
  public double KijunSen { get; set; }
  public double SenkouSpanA { get; set; }
  public double SenkouSpanB { get; set; }
  public double ChikouSpan { get; set; }
  public IchimokuPosition Position { get; set; }
}

public enum IchimokuPosition
{
  AboveCloud,
  BelowCloud,
  InsideCloud,
  TKCrossBullish,
  TKCrossBearish
}
