namespace StockIndicators.Domain.Models.Indicators;

/// <summary>Zone a bounded oscillator currently sits in, relative to its thresholds.</summary>
public enum ZoneState
{
  Neutral = 0,
  Overbought,
  Oversold
}
