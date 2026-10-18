namespace StockIndicators.Domain.Models.Rules;

/// <summary>
/// Action to execute when the rule group evaluates to true (ThenTrue) or false (ThenFalse).
/// </summary>
public class RuleAction
{
  public Guid Id { get; set; }

  /// <summary>Action type</summary>
  public ActionType Type { get; set; }

  /// <summary>Notification template to use (when Type = SendNotification)</summary>
  public Guid? NotificationTemplateId { get; set; }

  /// <summary>Serialized action-specific parameters</summary>
  public string? Parameters { get; set; }
}

/// <summary>
/// Types of actions that can be triggered by a rule.
/// </summary>
public enum ActionType
{
  SendNotification,
  DisableSubscription
}
