using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Projection of the (rule, ticker, indicator) triples of a rule's WHEN tree: the rule
/// itself keeps conditions in the <c>RuleData</c> jsonb only, so without this row set
/// "which instruments does the user watch with SMA" could not be answered in SQL.
/// Maintained by <c>RuleRepository</c> next to <see cref="RuleInstrument"/> and seeded
/// from existing rules by the AddInstrumentsSearch migration.
/// </summary>
public class RuleIndicator
{
  public Guid RuleId { get; set; }

  public string Ticker { get; set; } = null!;

  /// <summary>Empty string means "no exchange" (same convention as <see cref="RuleInstrument"/>).</summary>
  public string ExchangeName { get; set; } = string.Empty;

  public Indicator Indicator { get; set; }
}
