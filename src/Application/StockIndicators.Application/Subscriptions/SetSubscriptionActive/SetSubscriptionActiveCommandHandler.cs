using MediatR;
using Serilog;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.SetSubscriptionActive;

public class SetSubscriptionActiveCommandHandler : ICommandHandler<SetSubscriptionActiveCommand, Unit>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ILogger _logger;

  public SetSubscriptionActiveCommandHandler(ISubscriptionRepository subscriptionRepository, ILogger logger)
  {
    _subscriptionRepository = subscriptionRepository;
    _logger = logger.ForContext<SetSubscriptionActiveCommandHandler>();
  }

  public async Task<Unit> Handle(SetSubscriptionActiveCommand request, CancellationToken cancellationToken)
  {
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);

    if (request.IsActive)
      await ActivateAsync(subscription, cancellationToken);
    else
      await DeactivateAsync(subscription, cancellationToken);

    return Unit.Value;
  }

  private async Task ActivateAsync(Subscription subscription, CancellationToken cancellationToken)
  {
    // Explicit set is idempotent - skip the write when the subscription is already running
    if (subscription.Status == SubscriptionStatus.Active)
      return;

    var originalStatus = subscription.Status;

    await _subscriptionRepository.SetActiveAsync(subscription, true, cancellationToken);

    _logger.Information(
      "Subscription {SubscriptionId} activated: {OriginalStatus} -> Active",
      subscription.Id, originalStatus);
  }

  private async Task DeactivateAsync(Subscription subscription, CancellationToken cancellationToken)
  {
    if (subscription.Status == SubscriptionStatus.Inactive)
      return;

    var originalStatus = subscription.Status;

    await _subscriptionRepository.SetActiveAsync(subscription, false, cancellationToken);

    _logger.Information(
      "Subscription {SubscriptionId} deactivated: {OriginalStatus} -> Inactive",
      subscription.Id, originalStatus);
  }
}
