namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// A user's alert subscription: a pure container that binds a rule to a notification
/// cooldown and outputs. It carries no instrument/indicator fields — those live on
/// the rule's WHEN conditions (a rule may watch several instruments at once).
/// </summary>
public class Subscription : UpdatableEntity<Guid>
{
  public Guid UserId { get; set; }
  public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

  /// <summary>
  /// Alert cooldown: after an alert fires the subscription is skipped by the check
  /// pipeline until this much time has passed (minimum gap between two notifications).
  /// </summary>
  public TimeSpan Cooldown { get; set; }

  /// <summary>
  /// Cooldown in total seconds. Used for JSON Patch operations and API serialization.
  /// </summary>
  public int CooldownSeconds
  {
    get => (int)Cooldown.TotalSeconds;
    set => Cooldown = TimeSpan.FromSeconds(value);
  }

  /// <summary>
  /// When true (the default) the check pipeline never alerts while the newest candle a
  /// condition is evaluated on belongs to an extended session (pre-market, after-hours,
  /// overnight) of its exchange. Instruments of 24/7 exchanges (crypto) have no extended
  /// windows, so the flag is a no-op for them.
  /// </summary>
  public bool IgnoreExtendedSessions { get; set; } = true;

  public ICollection<SubscriptionOutput> Outputs { get; set; } = [];

  /// <summary>
  /// Timestamp of the last alert notification sent for this subscription.
  /// </summary>
  public DateTime? LastTriggeredAt { get; set; }

  /// <summary>
  /// Timestamp of the last time the user opened this subscription's alert. An alert is
  /// "new" while <see cref="LastTriggeredAt"/> is set and newer than this value.
  /// </summary>
  public DateTime? LastReadAt { get; set; }

  /// <summary>
  /// Rule (root group Id = Rule.Id) this subscription is evaluated by. Required: every
  /// subscription is a container of a rule, evaluated through the rule engine.
  /// </summary>
  public Guid RuleGroupId { get; set; }
}
