using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Subscriptions.SetSubscriptionActive;

public class SetSubscriptionActiveCommand : ICommand<Unit>, IHasUserId
{
  public Guid SubscriptionId { get; set; }
  public Guid UserId { get; set; }
  public bool IsActive { get; set; }

  public SetSubscriptionActiveCommand(Guid subscriptionId, Guid userId, bool isActive)
  {
    SubscriptionId = subscriptionId;
    UserId = userId;
    IsActive = isActive;
  }
}
