using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Subscriptions.DeleteSubscription;

public class DeleteSubscriptionCommand : ICommand<Unit>
{
  public Guid SubscriptionId { get; set; }
  public Guid UserId { get; set; }

  public DeleteSubscriptionCommand(Guid subscriptionId, Guid userId)
  {
    SubscriptionId = subscriptionId;
    UserId = userId;
  }
}