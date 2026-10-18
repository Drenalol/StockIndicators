using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Charts;

/// <summary>
/// What a trigger's chart has to draw, or why there is nothing to draw. The failure rides as a
/// stable code (the frontend localizes it, the raw code stays greppable in a log line).
/// </summary>
public sealed record TriggerChartResolution
{
  /// <summary>The alert was collected without a met condition, so no condition was ever recorded.</summary>
  public const string NoFiredConditionCode = "chart_unavailable:no_fired_condition";

  /// <summary>A pre-snapshot row whose condition id no longer exists in the rule tree.</summary>
  public const string ConditionRemovedCode = "chart_unavailable:condition_removed";

  public TriggerChartContext? Context { get; init; }

  public string? FailureCode { get; init; }

  public bool IsRenderable => Context is not null;

  public static TriggerChartResolution Of(TriggerChartContext context) => new() { Context = context };

  public static TriggerChartResolution NoFiredCondition() => new() { FailureCode = NoFiredConditionCode };

  public static TriggerChartResolution ConditionRemoved() => new() { FailureCode = ConditionRemovedCode };
}

public static class TriggerChartContextResolver
{
  /// <summary>
  /// Rebuilds the chart context of a trigger. The fired condition comes from the snapshot the
  /// alert stored on its history row, so a later edit of the rule (a rebuilt condition tree,
  /// retuned parameters) can neither redraw nor orphan the chart of an already delivered alert.
  /// Rows written before the snapshot existed fall back to <see cref="TriggerHistory.TriggeredConditionId"/>
  /// resolved against the live rule; a condition the user has since removed leaves nothing to
  /// draw, and the reason is both logged and returned as a stable code.
  /// </summary>
  public static async Task<TriggerChartResolution> ResolveAsync(
    IRuleRepository ruleRepository,
    ISerializer serializer,
    ILogger logger,
    TriggerHistory trigger,
    Subscription subscription,
    CancellationToken cancellationToken)
  {
    if (StoredFiredCondition(trigger, serializer) is { } snapshot)
      return TriggerChartResolution.Of(ToContext(snapshot));

    if (trigger.TriggeredConditionId is not { } conditionId)
    {
      // A notification collected on a false branch has no met condition, so there was never a
      // condition to draw - routine, not a fault
      logger.Debug(
        "Trigger chart for trigger {TriggerId} (subscription {SubscriptionId}) records no fired condition",
        trigger.Id.ToString(),
        subscription.Id.ToString()
      );

      return TriggerChartResolution.NoFiredCondition();
    }

    var rule = await ruleRepository.GetByIdAsync(subscription.RuleGroupId, cancellationToken);
    var condition = rule?.When.FindCondition(conditionId);

    if (condition is null)
    {
      // Only reachable for rows written before the snapshot: every new alert carries its own copy
      logger.Warning(
        "Trigger chart for trigger {TriggerId} (subscription {SubscriptionId}) is not renderable: the fired condition {ConditionId} is no longer present in rule {RuleId}",
        trigger.Id.ToString(),
        subscription.Id.ToString(),
        conditionId.ToString(),
        subscription.RuleGroupId.ToString()
      );

      return TriggerChartResolution.ConditionRemoved();
    }

    return TriggerChartResolution.Of(ToContext(condition));
  }

  /// <summary>
  /// The condition copied onto the history row at alert time; null when the row carries none
  /// (a legacy row) or the body does not describe a condition of an instrument.
  /// </summary>
  private static RuleCondition? StoredFiredCondition(TriggerHistory trigger, ISerializer serializer)
  {
    if (string.IsNullOrWhiteSpace(trigger.FiredConditionData))
      return null;

    if (serializer.DeserializeOrNull(trigger.FiredConditionData, typeof(RuleCondition)) is not RuleCondition condition)
      return null;

    // A condition without an instrument has nothing to chart, exactly as if it were not stored
    return string.IsNullOrWhiteSpace(condition.Ticker) ? null : condition;
  }

  private static TriggerChartContext ToContext(RuleCondition condition) =>
    new(condition.Indicator, condition.TemplateData, condition.Ticker, condition.ExchangeName);
}
