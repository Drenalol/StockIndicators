using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.DeleteSubscriptionsByInstrument;

public class DeleteSubscriptionsByInstrumentCommandHandler : ICommandHandler<DeleteSubscriptionsByInstrumentCommand, Unit>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly IRuleRepository _ruleRepository;

  public DeleteSubscriptionsByInstrumentCommandHandler(ISubscriptionRepository subscriptionRepository, IRuleRepository ruleRepository)
  {
    _subscriptionRepository = subscriptionRepository;
    _ruleRepository = ruleRepository;
  }

  public async Task<Unit> Handle(DeleteSubscriptionsByInstrumentCommand request, CancellationToken cancellationToken)
  {
    var ruleIds = await _subscriptionRepository.GetRuleGroupIdsByInstrumentAsync(request.Ticker, request.ExchangeName, request.UserId, cancellationToken);

    await _subscriptionRepository.DeleteByInstrumentAsync(request.Ticker, request.ExchangeName, request.UserId, cancellationToken);
    await _subscriptionRepository.CommitAsync(cancellationToken);

    foreach (var ruleId in ruleIds)
    {
      if (await _ruleRepository.IsReferencedBySubscriptionAsync(ruleId, cancellationToken))
        continue;

      await _ruleRepository.DeleteAsync(ruleId, cancellationToken);
      await _ruleRepository.CommitAsync(cancellationToken);
    }

    return Unit.Value;
  }
}
