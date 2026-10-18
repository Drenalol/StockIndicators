using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Subscriptions.MarkAllSubscriptionsRead;

public class MarkAllSubscriptionsReadCommand : ICommand<Unit>, IHasUserId
{
  public Guid UserId { get; set; }

  public MarkAllSubscriptionsReadCommand(Guid userId) => UserId = userId;
}
