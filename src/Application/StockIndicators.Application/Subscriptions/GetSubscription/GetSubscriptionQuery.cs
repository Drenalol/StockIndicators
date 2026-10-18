using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Subscriptions.GetSubscription;

public class GetSubscriptionQuery : IQuery<SubscriptionDto>
{
  public Guid SubscriptionId { get; }
  public Guid UserId { get; set; }

  public GetSubscriptionQuery(Guid subscriptionId, Guid userId)
  {
    SubscriptionId = subscriptionId;
    UserId = userId;
  }
}