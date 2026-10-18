using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.Adx;

public class AdxIndicatorResult : IndicatorResult
{
  public double Adx { get; set; }
  public double DmPlus { get; set; }
  public double DmMinus { get; set; }
  public AdxStrength Strength { get; set; }
}

public enum AdxStrength
{
  Weak,
  Moderate,
  Strong,
  VeryStrong
}
