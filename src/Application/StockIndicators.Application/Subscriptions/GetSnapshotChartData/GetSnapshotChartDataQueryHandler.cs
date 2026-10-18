using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetSnapshotChartData;

public class GetSnapshotChartDataQueryHandler : IQueryHandler<GetSnapshotChartDataQuery, ChartData?>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ITriggerSnapshotRepository _snapshotRepository;
  private readonly IRuleRepository _ruleRepository;
  private readonly ISubscriptionCache _subscriptionCache;
  private readonly ITriggerChartService _triggerChartService;
  private readonly IUserLocaleProvider _localeProvider;

  public GetSnapshotChartDataQueryHandler(
    ISubscriptionRepository subscriptionRepository,
    ITriggerSnapshotRepository snapshotRepository,
    IRuleRepository ruleRepository,
    ISubscriptionCache subscriptionCache,
    ITriggerChartService triggerChartService,
    IUserLocaleProvider localeProvider
  )
  {
    _subscriptionRepository = subscriptionRepository;
    _snapshotRepository = snapshotRepository;
    _ruleRepository = ruleRepository;
    _subscriptionCache = subscriptionCache;
    _triggerChartService = triggerChartService;
    _localeProvider = localeProvider;
  }

  public async Task<ChartData?> Handle(GetSnapshotChartDataQuery request, CancellationToken cancellationToken)
  {
    // The snapshot row is reachable only through its subscription: a foreign subscription id
    // fails the ownership check before the row lookup (404)
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);
    var row = await _snapshotRepository.GetByIdAsync(request.SnapshotId, subscription.Id, cancellationToken);

    if (row is null)
      return null;

    var rule = await _ruleRepository.GetByIdAsync(subscription.RuleGroupId, cancellationToken);

    if (rule is null)
      return null;

    // The stored row says which (instrument, timeframe) group to draw; the conditions of that
    // group are re-read from the current rule (same dedup as the snapshot renderer), so an
    // indicator edit shows up in the interactive view the same instant it deletes the rows
    var conditions = SubscriptionSnapshotRenderer
      .EnumerateLeafConditions(rule.When)
      .Where(condition =>
        condition.Ticker == row.Ticker &&
        (condition.ExchangeName ?? string.Empty) == row.ExchangeName &&
        condition.TimeFrame == row.TimeFrame)
      .GroupBy(condition => (condition.Indicator, condition.TemplateData))
      .Select(distinct => distinct.First())
      .ToArray();

    if (conditions.Length == 0)
      return null;

    var showTriggerMarker = await _subscriptionCache.IsTriggeredAsync(subscription, cancellationToken);

    var context = new SnapshotChartContext(
      row.Ticker,
      row.ExchangeName.Length > 0 ? row.ExchangeName : null,
      row.TimeFrame,
      conditions.Select(condition => new TriggerChartContext(condition.Indicator, condition.TemplateData)).ToArray(),
      showTriggerMarker ? subscription.LastTriggeredAt : null);

    var locale = await _localeProvider.GetAsync(subscription.UserId, cancellationToken);

    return await _triggerChartService.RenderSnapshotDataAsync(subscription, context, locale, cancellationToken);
  }
}
