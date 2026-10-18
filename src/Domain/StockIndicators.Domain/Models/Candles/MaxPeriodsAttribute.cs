namespace StockIndicators.Domain.Models.Candles;

[AttributeUsage(AttributeTargets.Field)]
public class MaxPeriodsAttribute : Attribute
{
  public int Max { get; }

  public MaxPeriodsAttribute(int max) => Max = max;
}