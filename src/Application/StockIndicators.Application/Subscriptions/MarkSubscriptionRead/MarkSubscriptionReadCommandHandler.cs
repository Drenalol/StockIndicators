using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.MarkSubscriptionRead;

public class MarkSubscriptionReadCommandHandler : ICommandHandler<MarkSubscriptionReadCommand, Unit>
{
  private readonly ISubscriptionRepository _subscriptionRepository;

  public MarkSubscriptionReadCommandHandler(ISubscriptionRepository subscriptionRepository) => _subscriptionRepository = subscriptionRepository;

  public async Task<Unit> Handle(MarkSubscriptionReadCommand request, CancellationToken cancellationToken)
  {
    // GetByIdAsync enforces ownership (throws SubscriptionNotFoundException otherwise)
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);

    // Clearing the "new" flag is idempotent — skip the write when nothing is unread
    if (subscription.LastTriggeredAt is null || subscription.LastReadAt >= subscription.LastTriggeredAt)
      return Unit.Value;

    await _subscriptionRepository.MarkReadAsync(subscription, cancellationToken);

    return Unit.Value;
  }
}
