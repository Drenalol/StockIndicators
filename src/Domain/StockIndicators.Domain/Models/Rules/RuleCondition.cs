using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Represents a single alert condition: "indicator vs reference when operator".
/// Example: SMA(20, Close) crosses_above EMA(50, Close) for AAPL on NMS.
/// </summary>
public class RuleCondition
{
  public Guid Id { get; set; }

  /// <summary>Primary indicator being monitored (SMA, EMA, StochRSI, etc.)</summary>
  public Indicator Indicator { get; set; }

  /// <summary>Indicator parameters (periods, candle part, etc.) — serialized template data</summary>
  public string? TemplateData { get; set; }

  /// <summary>Comparison operator (crosses_above, greater_than, etc.)</summary>
  public ConditionOperator Operator { get; set; }

  /// <summary>Reference type: another indicator, raw price (OHLC), or static value</summary>
  public ReferenceType ReferenceType { get; set; }

  /// <summary>Reference indicator (when ReferenceType = Indicator). Null otherwise.</summary>
  public Indicator? ReferenceIndicator { get; set; }

  /// <summary>Reference indicator template data (when ReferenceType = Indicator)</summary>
  public string? ReferenceTemplateData { get; set; }

  /// <summary>Reference candle part (when ReferenceType = Price). Null otherwise.</summary>
  public CandlePart? ReferenceCandlePart { get; set; }

  /// <summary>Reference static value (when ReferenceType = Value). Null otherwise.</summary>
  public decimal? ReferenceValue { get; set; }

  /// <summary>Deviation threshold for WithinDeviation operator. Null otherwise.</summary>
  public decimal? Deviation { get; set; }

  /// <summary>Instrument ticker to apply this condition to</summary>
  public string Ticker { get; set; } = null!;

  /// <summary>Exchange identifier (NMS, NYQ, MOEX, CCC, etc.)</summary>
  public string? ExchangeName { get; set; }

  /// <summary>Time frame for evaluating this condition</summary>
  public TimeFrame TimeFrame { get; set; }
}

/// <summary>
/// Type of the reference value to compare against.
/// </summary>
public enum ReferenceType
{
  /// <summary>Compare against another indicator value</summary>
  Indicator,

  /// <summary>Compare against raw price (Open, High, Low, Close)</summary>
  Price,

  /// <summary>Compare against a static numeric value</summary>
  Value
}
