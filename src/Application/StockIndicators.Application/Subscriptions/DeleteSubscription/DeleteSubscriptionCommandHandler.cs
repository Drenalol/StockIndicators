using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.DeleteSubscription;

public class DeleteSubscriptionCommandHandler : ICommandHandler<DeleteSubscriptionCommand, Unit>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly IRuleRepository _ruleRepository;

  public DeleteSubscriptionCommandHandler(ISubscriptionRepository subscriptionRepository, IRuleRepository ruleRepository)
  {
    _subscriptionRepository = subscriptionRepository;
    _ruleRepository = ruleRepository;
  }

  public async Task<Unit> Handle(DeleteSubscriptionCommand request, CancellationToken cancellationToken)
  {
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);
    var ruleId = subscription.RuleGroupId;

    await _subscriptionRepository.DeleteAsync(request.SubscriptionId, request.UserId, cancellationToken);
    await _subscriptionRepository.CommitAsync(cancellationToken);

    await DeleteOrphanedRuleAsync(ruleId, cancellationToken);

    return Unit.Value;
  }

  private async Task DeleteOrphanedRuleAsync(Guid? ruleId, CancellationToken cancellationToken)
  {
    if (ruleId is not { } id)
      return;

    if (await _ruleRepository.IsReferencedBySubscriptionAsync(id, cancellationToken))
      return;

    await _ruleRepository.DeleteAsync(id, cancellationToken);
    await _ruleRepository.CommitAsync(cancellationToken);
  }
}