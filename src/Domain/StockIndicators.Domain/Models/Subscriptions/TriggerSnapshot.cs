using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// A snapshot chart row of a subscription: one row per (instrument, timeframe) group
/// of the subscription's indicator conditions. The row carries the group's identity,
/// legend names and the freshness anchor - the picture is computed from the live candle
/// cache on every chart request. Only the latest set is kept (replaced on every refresh);
/// rule edits delete the rows, so the next page visit resolves them free with the current
/// settings.
/// </summary>
public class TriggerSnapshot : Entity<Guid>
{
  public Guid SubscriptionId { get; set; }
  public string Ticker { get; set; } = null!;

  /// <summary>Empty string means "no exchange" (the RuleInstruments convention).</summary>
  public string ExchangeName { get; set; } = string.Empty;

  public TimeFrame TimeFrame { get; set; }

  /// <summary>Legend signatures of the merged indicator conditions, ";"-joined (e.g. "RSI; EMA 21").</summary>
  public string IndicatorNames { get; set; } = null!;

  /// <summary>UTC date of the newest candle in the rendered picture; null in rows stored
  /// before the field existed (the client then cannot judge freshness and stays silent).</summary>
  public DateTime? LastCandleAt { get; set; }

  public DateTime RenderedAt { get; set; }
}
