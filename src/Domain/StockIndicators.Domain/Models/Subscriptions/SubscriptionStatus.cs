namespace StockIndicators.Domain.Models.Subscriptions;

/// <summary>
/// Subscription lifecycle status. The user's own switch is the only thing that moves a
/// subscription here: the hosted version additionally had a system pause for subscriptions
/// that carried premium features after the plan lapsed, and there is no plan to lapse now.
/// </summary>
public enum SubscriptionStatus
{
  /// <summary>Turned off by the user.</summary>
  Inactive = 0,

  /// <summary>Running: the check pipeline evaluates it and alerts fire.</summary>
  Active = 1,
}
