using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Rules.UpdateRule;

public class UpdateRuleCommandHandler : ICommandHandler<UpdateRuleCommand, Unit>
{
  private readonly IRuleRepository _ruleRepository;
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ITriggerSnapshotRepository _snapshotRepository;
  private readonly ISerializer _serializer;
  private readonly ITrailingStopAnchorStore? _anchorStore;

  public UpdateRuleCommandHandler(
    IRuleRepository ruleRepository,
    ISubscriptionRepository subscriptionRepository,
    ITriggerSnapshotRepository snapshotRepository,
    ISerializer serializer,
    ITrailingStopAnchorStore? anchorStore = null
  )
  {
    _ruleRepository = ruleRepository;
    _subscriptionRepository = subscriptionRepository;
    _snapshotRepository = snapshotRepository;
    _serializer = serializer;
    _anchorStore = anchorStore;
  }

  public async Task<Unit> Handle(UpdateRuleCommand request, CancellationToken cancellationToken)
  {
    RuleValidation.Validate(request.When);
    RuleValidation.ValidateName(request.Name);
    RuleValidation.ValidateActions(request.ThenTrue, request.ThenFalse);
    RuleValidation.ValidateConditions(_serializer, request.When);
    RuleValidation.NormalizeConditionTimeFrames(_serializer, request.When);

    var rule = await _ruleRepository.GetByIdForUserAsync(request.Id, request.UserId, cancellationToken);

    if (rule == null)
      throw new RuleNotFoundException();

    // Snapshot before the projection is rebuilt: provider tracking has to drop the
    // instruments the old tree watched and nobody else's subscriptions watch anymore
    var previousInstruments = await _ruleRepository.GetInstrumentsAsync(rule.Id, cancellationToken);

    // Old leaf ids before the rebuild: conditions that disappear from the tree take their
    // persisted trailing-stop anchors with them (re-added conditions keep their client-sent
    // ids and therefore their anchors)
    var previousConditionIds = new HashSet<Guid>();
    RuleMappers.CollectConditionIds(rule.When, previousConditionIds);

    // Rebuild the tree under the stable rule id (root group Id = Rule.Id)
    rule.Name = request.Name;
    rule.When = RuleMappers.GroupToDomain(request.When, rule.Id);
    rule.ThenTrue = (request.ThenTrue ?? []).Select(RuleMappers.ActionToDomain).ToList();
    rule.ThenFalse = (request.ThenFalse ?? []).Select(RuleMappers.ActionToDomain).ToList();
    rule.RuleData = _serializer.Serialize(RuleMappers.ToPayload(rule));
    rule.Updated = DateTime.UtcNow;

    // The rule was loaded AsNoTracking (ownership-scoped read); without UpdateAsync the
    // rebuilt tree would never reach SaveChanges and PATCH would silently discard it
    await _ruleRepository.UpdateAsync(rule, cancellationToken);
    await _ruleRepository.CommitAsync(cancellationToken);

    await _subscriptionRepository.SyncInstrumentsAfterRuleChangeAsync(previousInstruments, cancellationToken);

    // A saved rule changes the snapshot's conditions; drop the stored charts so the next
    // page visit re-renders them free with the current settings
    await _snapshotRepository.DeleteByRuleIdAsync(rule.Id, cancellationToken);

    if (_anchorStore is not null)
    {
      var currentConditionIds = new HashSet<Guid>();
      RuleMappers.CollectConditionIds(rule.When, currentConditionIds);

      previousConditionIds.ExceptWith(currentConditionIds);
      await _anchorStore.RemoveAnchorsAsync(previousConditionIds, cancellationToken);
    }

    return Unit.Value;
  }
}
