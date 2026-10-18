namespace StockIndicators.Domain.Models.Indicators;

/// <summary>Where price sits relative to a high/low channel (Keltner, Donchian).</summary>
public enum ChannelPosition
{
  Inside = 0,
  AboveUpper,
  BelowLower
}
