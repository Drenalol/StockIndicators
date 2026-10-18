using System.Diagnostics;
using MediatR;
using Serilog;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Application.Subscriptions.CheckSubscriptions;

public class CheckSubscriptionsCommandHandler : IBackgroundCommandHandler<CheckSubscriptionsCommand>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly IAlertDispatcher _alertAggregator;
  private readonly ITriggerHistoryRepository _triggerHistoryRepository;
  private readonly IRuleRepository _ruleRepository;
  private readonly IRuleEvaluator _ruleEvaluator;
  private readonly IRuleActionExecutor _ruleActionExecutor;
  private readonly IUserLocaleProvider _localeProvider;
  private readonly ILogger _logger;

  public CheckSubscriptionsCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    IAlertDispatcher alertAggregator,
    ITriggerHistoryRepository triggerHistoryRepository,
    IRuleRepository ruleRepository,
    IRuleEvaluator ruleEvaluator,
    IRuleActionExecutor ruleActionExecutor,
    IUserLocaleProvider localeProvider,
    ILogger logger
  )
  {
    _subscriptionRepository = subscriptionRepository;
    _alertAggregator = alertAggregator;
    _triggerHistoryRepository = triggerHistoryRepository;
    _ruleRepository = ruleRepository;
    _ruleEvaluator = ruleEvaluator;
    _ruleActionExecutor = ruleActionExecutor;
    _localeProvider = localeProvider;
    _logger = logger;
  }

  public async Task Handle(CheckSubscriptionsCommand request, CancellationToken cancellationToken)
  {
    var stopwatch = Stopwatch.StartNew();

    // One lookup for the whole batch: a subscription whose rule watches several instruments
    // of this batch is still checked (and can alert) exactly once
    var subscriptions = await _subscriptionRepository.GetRuleSubscriptionsByInstrumentsAsync(request.Instruments, cancellationToken);

    BusinessMetrics.SubscriptionsCheckedCount(subscriptions.Length);
    _logger.Debug("{Instruments} has {Subs} rule subscriptions", request.Instruments.Count, subscriptions.Length);

    foreach (var subscription in subscriptions)
    {
      if (await _subscriptionRepository.IsTriggeredAsync(subscription, cancellationToken))
        continue;

      try
      {
        await HandleSubscriptionAsync(subscription, cancellationToken);
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Rule subscription {Id} failed", subscription.Id.ToString());
      }
    }

    await _alertAggregator.FlushAsync(cancellationToken);
    await _triggerHistoryRepository.CommitAsync(cancellationToken);

    BusinessMetrics.CheckBatchDurationSeconds(stopwatch.Elapsed.TotalSeconds);
  }

  private async Task HandleSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken)
  {
    var rule = await _ruleRepository.GetByIdAsync(subscription.RuleGroupId, cancellationToken);

    // The rule was deleted; the Restrict FK prevents this while subscriptions reference it,
    // but a concurrent delete may have removed the last ones and this row raced in between
    if (rule is null)
      return;

    var locale = await _localeProvider.GetAsync(subscription.UserId, cancellationToken);

    // The subscription's extended-sessions opt-out travels into the leaf evaluator: by
    // default a condition resting on a pre/post-market candle is silently unmet
    var evaluation = await _ruleEvaluator.EvaluateGroupAsync(
      rule.When, locale, subscription.IgnoreExtendedSessions, cancellationToken);

    if (evaluation.Met)
      BusinessMetrics.RuleTriggered();

    var actionsRan = await _ruleActionExecutor.ExecuteAsync(rule, evaluation, subscription, locale, cancellationToken);

    if (actionsRan)
    {
      await _subscriptionRepository.SetTriggeredAsync(subscription, cancellationToken);
      _logger.Information("Rule {RuleId} ({Name}) {Met} for subscription {Id}", rule.Id.ToString(), rule.Name, evaluation.Met, subscription.Id.ToString());
    }
  }
}
