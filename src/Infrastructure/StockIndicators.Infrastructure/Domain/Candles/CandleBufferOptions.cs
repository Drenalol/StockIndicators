namespace StockIndicators.Infrastructure.Domain.Candles;

public class CandleBufferOptions
{
  public int BatchTriggerDelaySec { get; set; } = 60;
}