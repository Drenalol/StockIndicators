using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Subscriptions.GetSubscription;

public class SubscriptionNotFoundException : NotFoundException
{
  public SubscriptionNotFoundException() : base("Subscription not found")
  {
  }
}