using Newtonsoft.Json;
using StockIndicators.Contracts.Rules;
using StockIndicators.Contracts.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetSubscription;

/// <summary>
/// Subscription view: an alert container (cooldown + outputs) bound to a rule. It carries
/// no indicator/instrument fields — those live on <see cref="Rule"/>'s WHEN conditions;
/// clients read the "primary" instrument from the first condition.
/// </summary>
public class SubscriptionDto
{
  public Guid Id { get; set; }

  /// <summary>User's switch plus the plan pause: see <see cref="SubscriptionStatusDto"/>.</summary>
  public SubscriptionStatusDto Status { get; set; }

  [JsonIgnore]
  public TimeSpan? Cooldown { get; set; }

  /// <summary>Total seconds of the cooldown, for JSON serialization.</summary>
  public int? CooldownSeconds => Cooldown?.TotalSeconds is double s ? (int)s : null;

  /// <summary>Deprecated alias of <see cref="CooldownSeconds"/> for clients shipped before the Interval-to-Cooldown rename.</summary>
  public int? IntervalSeconds => CooldownSeconds;

  /// <summary>
  /// False only when the user opted into alerts on extended-session candles
  /// (pre-market, after-hours, overnight). Crypto instruments never have them.
  /// </summary>
  public bool IgnoreExtendedSessions { get; set; } = true;

  public List<SubscriptionOutputDto> Outputs { get; set; } = [];

  public DateTime Created { get; set; }

  public DateTime? Updated { get; set; }

  /// <summary>When the last alert notification was sent; the alert is "new" while it is
  /// newer than <see cref="LastReadAt"/>.</summary>
  public DateTime? LastTriggeredAt { get; set; }

  /// <summary>When the user last opened this subscription's alert (cleared the "new" flag).</summary>
  public DateTime? LastReadAt { get; set; }

  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public Guid? UserId { get; set; }

  /// <summary>Rule (root group id) this subscription is a container of.</summary>
  public Guid RuleGroupId { get; set; }

  /// <summary>Full rule (WHEN-THEN) — the source of instruments, indicators and parameters.</summary>
  public RuleDto? Rule { get; set; }
}
