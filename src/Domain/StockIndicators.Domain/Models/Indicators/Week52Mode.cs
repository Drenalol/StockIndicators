namespace StockIndicators.Domain.Models.Indicators;

/// <summary>
/// What fires the 52-week condition: a fresh extreme (the close steps over the previous
/// 52-week high/low) or a retreat/rally measured as a percent pullback from that extreme.
/// </summary>
public enum Week52Mode
{
  NewExtreme = 0,
  Pullback
}
