using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;
using StockIndicators.Contracts.Rules;

namespace StockIndicators.Controllers;

public class SubscriptionCreateRequest
{
  /// <summary>Alert cooldown in seconds (minimum gap between two notifications).</summary>
  public int? CooldownSeconds { get; set; }

  /// <summary>
  /// Deprecated alias of <see cref="CooldownSeconds"/>: apps shipped before the
  /// Interval-to-Cooldown rename still send it. Removed when those clients retire.
  /// </summary>
  public int? IntervalSeconds { get; set; }

  /// <summary>The cooldown the request resolves to (new name wins; 0 when neither is given).</summary>
  public int ResolvedCooldownSeconds => CooldownSeconds ?? IntervalSeconds ?? 0;

  /// <summary>Alert outputs (channel + template + push target). Defaults to Telegram when omitted.</summary>
  public List<SubscriptionOutputRequest>? Outputs { get; set; }

  /// <summary>
  /// The rule (WHEN-THEN with AND/OR) this subscription is a container of. Required: the
  /// subscription itself stores no indicator/instrument — those are the rule's conditions.
  /// </summary>
  [Required]
  public CreateRuleRequest Rule { get; set; } = null!;

  /// <summary>
  /// Optional instrument identity resolved by the client during instrument selection
  /// (source + exchange from the search/list response). Lets the server confirm the
  /// instrument from the warm cache instead of querying providers on the request path.
  /// The exchange must match the query's exchangeName — a mismatch is rejected as
  /// NotFound rather than falling through to the slow provider iteration.
  /// </summary>
  public InstrumentIdentityRequest? Instrument { get; set; }

  /// <summary>
  /// When false the subscription also alerts on signals coming from extended-session
  /// candles (pre-market, after-hours, overnight). Defaults to true (extended candles
  /// never alert); a no-op on 24/7 crypto instruments, which have no extended windows.
  /// </summary>
  public bool? IgnoreExtendedSessions { get; set; }
}

public class InstrumentIdentityRequest
{
  [Required]
  public string Source { get; set; } = null!;

  public string? ExchangeName { get; set; }
}
