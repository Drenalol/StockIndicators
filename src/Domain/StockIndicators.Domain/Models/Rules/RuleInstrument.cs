using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Relational projection of the instruments (ticker + exchange) a rule's WHEN tree watches.
/// Maintained whenever the rule is created or updated; it is what lets the check pipeline
/// find subscriptions by an arrived candle instrument, providers track every rule instrument
/// live, and instruments of deleted subscriptions be untracked without parsing RuleData.
/// <see cref="ExchangeName"/> is the empty string when the instrument carries no exchange.
/// </summary>
public class RuleInstrument
{
  public Guid RuleId { get; set; }

  public string Ticker { get; set; } = null!;

  public string ExchangeName { get; set; } = string.Empty;

  public InstrumentKey ToKey() => new(Ticker, ExchangeName.Length > 0 ? ExchangeName : null);
}
