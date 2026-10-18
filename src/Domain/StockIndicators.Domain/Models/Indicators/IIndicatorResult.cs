using System.Text;

namespace StockIndicators.Domain.Models.Indicators;

public class IndicatorResult
{
  public bool IsTriggered { get; set; }

  public void Clear() => Set(false);

  public IndicatorResult Set(bool triggered = true)
  {
    IsTriggered = triggered;
    return this;
  }
}

public static class IndicatorResultFactory<TResult> where TResult : IndicatorResult, new()
{
  public static readonly TResult False = new() { IsTriggered = false };

  public static readonly TResult True = new() { IsTriggered = true };
}
