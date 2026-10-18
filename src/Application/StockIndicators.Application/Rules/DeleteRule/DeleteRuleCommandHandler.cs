using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Rules.DeleteRule;

public class DeleteRuleCommandHandler : ICommandHandler<DeleteRuleCommand, Unit>
{
  private readonly IRuleRepository _ruleRepository;
  private readonly ITrailingStopAnchorStore? _anchorStore;

  public DeleteRuleCommandHandler(IRuleRepository ruleRepository, ITrailingStopAnchorStore? anchorStore = null)
  {
    _ruleRepository = ruleRepository;
    _anchorStore = anchorStore;
  }

  public async Task<Unit> Handle(DeleteRuleCommand request, CancellationToken cancellationToken)
  {
    var rule = await _ruleRepository.GetByIdForUserAsync(request.Id, request.UserId, cancellationToken);

    if (rule == null)
      throw new RuleNotFoundException();

    // Subscriptions referencing the rule fall back to their mirrored flat condition (FK SetNull)
    await _ruleRepository.DeleteAsync(rule.Id, cancellationToken);
    await _ruleRepository.CommitAsync(cancellationToken);

    // The rule's leaf conditions are gone, so are their persisted trailing-stop anchors
    if (_anchorStore is not null)
    {
      var conditionIds = new HashSet<Guid>();
      RuleMappers.CollectConditionIds(rule.When, conditionIds);
      await _anchorStore.RemoveAnchorsAsync(conditionIds, cancellationToken);
    }

    return Unit.Value;
  }
}
