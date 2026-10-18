namespace StockIndicators.Domain.Models.Indicators;

/// <summary>
/// Shared direction filter for directional indicator templates: restricts which
/// trigger events fire the alert so one-tap presets can watch a single side
/// (overbought only, bullish cross only, upper band only, gap up only).
/// The default <see cref="Both"/> keeps pre-direction rules behaving exactly as before.
/// </summary>
public enum AlertDirection
{
  Both = 0,
  Up,
  Down
}
