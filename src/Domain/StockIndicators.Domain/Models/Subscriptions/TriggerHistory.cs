namespace StockIndicators.Domain.Models.Subscriptions;

public class TriggerHistory : Entity<Guid>
{
  public Guid SubscriptionId { get; set; }
  public string TriggerValue { get; set; } = null!;

  /// <summary>
  /// Wall-clock moment the check pipeline detected the condition (UTC). Rows created before
  /// the detection-time change carry the trigger candle timestamp here instead.
  /// </summary>
  public DateTime TriggeredAt { get; set; }

  /// <summary>
  /// Timestamp of the candle the condition actually saw (UTC). The trigger chart renders its
  /// window around this instant, so a lagging candle cache cannot slide the marker past the
  /// candle that fired. Null for legacy rows, whose <see cref="TriggeredAt"/> is the candle.
  /// </summary>
  public DateTime? TriggeredCandleAt { get; set; }

  /// <summary>
  /// Id of the rule condition that fired the alert (rule-based subscriptions only).
  /// Lets on-demand chart re-renders show the triggered indicator instead of the
  /// subscription's primary one. Null for flat subscriptions or when the rule
  /// condition no longer exists.
  /// </summary>
  public Guid? TriggeredConditionId { get; set; }

  /// <summary>
  /// The fired rule condition serialized as it was configured at alert time (jsonb). The
  /// trigger chart renders from this snapshot, so editing the alert afterwards (a rebuilt
  /// condition tree, a changed indicator template) neither redraws nor orphans the chart of
  /// an alert the user already received. Null for rows written before the snapshot existed
  /// and for alerts that fired without a met condition; those fall back to
  /// <see cref="TriggeredConditionId"/> looked up in the live rule.
  /// </summary>
  public string? FiredConditionData { get; set; }

  public ICollection<TriggerDelivery> Deliveries { get; set; } = [];
}
