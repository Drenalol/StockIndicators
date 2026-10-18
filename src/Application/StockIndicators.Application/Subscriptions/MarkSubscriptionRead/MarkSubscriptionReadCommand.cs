using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Subscriptions.MarkSubscriptionRead;

public class MarkSubscriptionReadCommand : ICommand<Unit>, IHasUserId
{
  public Guid SubscriptionId { get; set; }
  public Guid UserId { get; set; }

  public MarkSubscriptionReadCommand(Guid subscriptionId, Guid userId)
  {
    SubscriptionId = subscriptionId;
    UserId = userId;
  }
}
