using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.MarkAllSubscriptionsRead;

public class MarkAllSubscriptionsReadCommandHandler : ICommandHandler<MarkAllSubscriptionsReadCommand, Unit>
{
  private readonly ISubscriptionRepository _subscriptionRepository;

  public MarkAllSubscriptionsReadCommandHandler(ISubscriptionRepository subscriptionRepository) => _subscriptionRepository = subscriptionRepository;

  public async Task<Unit> Handle(MarkAllSubscriptionsReadCommand request, CancellationToken cancellationToken)
  {
    await _subscriptionRepository.MarkAllReadAsync(request.UserId, cancellationToken);

    return Unit.Value;
  }
}
