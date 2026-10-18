using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Users;
using StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.RefreshSubscriptionSnapshot;

public sealed record RefreshSubscriptionSnapshotCommand(Guid SubscriptionId, Guid UserId) : ICommand<SubscriptionSnapshotDto>, IHasUserId;

public class RefreshSubscriptionSnapshotCommandHandler : ICommandHandler<RefreshSubscriptionSnapshotCommand, SubscriptionSnapshotDto>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ITriggerSnapshotRepository _snapshotRepository;
  private readonly IRuleRepository _ruleRepository;
  private readonly ISubscriptionSnapshotRenderer _renderer;
  private readonly ISubscriptionCache _subscriptionCache;

  public RefreshSubscriptionSnapshotCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    ITriggerSnapshotRepository snapshotRepository,
    IRuleRepository ruleRepository,
    ISubscriptionSnapshotRenderer renderer,
    ISubscriptionCache subscriptionCache
  )
  {
    _subscriptionRepository = subscriptionRepository;
    _snapshotRepository = snapshotRepository;
    _ruleRepository = ruleRepository;
    _renderer = renderer;
    _subscriptionCache = subscriptionCache;
  }

  public async Task<SubscriptionSnapshotDto> Handle(RefreshSubscriptionSnapshotCommand request, CancellationToken cancellationToken)
  {
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);

    var rule = await _ruleRepository.GetByIdAsync(subscription.RuleGroupId, cancellationToken);
    var showTriggerMarker = rule is not null && await _subscriptionCache.IsTriggeredAsync(subscription, cancellationToken);
    var rendered = rule is null ? null : await _renderer.RenderAsync(subscription, rule, showTriggerMarker, cancellationToken);

    if (rendered is null || rendered.Length == 0)
      throw new SnapshotNotFoundException();

    await _snapshotRepository.ReplaceAsync(subscription.Id, rendered, cancellationToken);

    return new SubscriptionSnapshotDto(
      subscription.Id,
      rendered.Select(SnapshotDtoMapper.ToDto).ToArray(),
      rendered.Max(row => row.RenderedAt));
  }
}
