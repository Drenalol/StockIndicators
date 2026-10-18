using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.EventBus;
using StockIndicators.Application.Notifications.Dispatcher;
using StockIndicators.Contracts.Events;
using StockIndicators.Contracts.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Executes the THEN-actions of a rule: SendNotification (multi-output alert pipeline)
/// and DisableSubscription. A failing action never blocks the remaining ones.
/// </summary>
public class RuleActionExecutor : IRuleActionExecutor
{
  /// <summary>
  /// Indicators whose condition, once met on a forming candle, cannot say anything new until
  /// the candle closes: the value accumulates monotonically within the candle (Volume only
  /// grows), so every re-fire on the same candle is a pure duplicate. Repeats of these are
  /// suppressed per (condition, candle); price-driven indicators oscillate within the candle
  /// and keep alerting on each cooldown. Gap already one-shots itself (previous-candle dedup).
  /// </summary>
  private static readonly HashSet<Indicator> SameCandleRepeatIsNoise = [Indicator.Volume];

  private readonly IAlertDispatcher _alertDispatcher;
  private readonly IEventBus _eventBus;
  private readonly ITriggerHistoryRepository _triggerHistoryRepository;
  private readonly ITriggerChartService _triggerChartService;
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ISerializer _serializer;
  private readonly ILogger _logger;

  public RuleActionExecutor(
    IAlertDispatcher alertDispatcher,
    IEventBus eventBus,
    ITriggerHistoryRepository triggerHistoryRepository,
    ITriggerChartService triggerChartService,
    ISubscriptionRepository subscriptionRepository,
    ISerializer serializer,
    ILogger logger
  )
  {
    _alertDispatcher = alertDispatcher;
    _eventBus = eventBus;
    _triggerHistoryRepository = triggerHistoryRepository;
    _triggerChartService = triggerChartService;
    _subscriptionRepository = subscriptionRepository;
    _serializer = serializer;
    _logger = logger;
  }

  public async Task<bool> ExecuteAsync(Rule rule, RuleEvaluationResult evaluation, Subscription subscription, UserLocale locale, CancellationToken cancellationToken = default)
  {
    var conditionMet = evaluation.Met;
    var actions = conditionMet ? rule.ThenTrue : rule.ThenFalse;

    if (actions.Count == 0)
      return false;

    var executed = new List<string>();

    foreach (var action in actions)
    {
      try
      {
        if (await ExecuteActionAsync(action, evaluation, subscription, rule, locale, cancellationToken))
          executed.Add(action.Type.ToString());
      }
      catch (Exception e)
      {
        _logger.Warning(e, "Rule {RuleId} action {Action} failed for subscription {SubscriptionId}", rule.Id.ToString(), action.Type, subscription.Id.ToString());
      }
    }

    if (executed.Count > 0)
      await PublishRuleEventAsync(rule, conditionMet, subscription, string.Join(", ", executed), cancellationToken);

    return executed.Count > 0;
  }

  /// <summary>
  /// Runs one action; returns false when the action was skipped as a same-candle duplicate,
  /// so a suppressed notification neither counts as executed nor raises the unread badge.
  /// </summary>
  private async Task<bool> ExecuteActionAsync(
    RuleAction action, RuleEvaluationResult evaluation, Subscription subscription, Rule rule, UserLocale locale, CancellationToken cancellationToken)
  {
    switch (action.Type)
    {
      case ActionType.SendNotification:
        return await SendNotificationAsync(rule, evaluation, subscription, locale, cancellationToken);

      case ActionType.DisableSubscription:
        await _subscriptionRepository.SetActiveAsync(subscription, false, cancellationToken);
        _logger.Information("Subscription {Id} disabled by rule {RuleId}", subscription.Id.ToString(), rule.Id.ToString());
        return true;

      default:
        _logger.Warning("Unknown rule action {Action} in rule {RuleId}", action.Type, rule.Id.ToString());
        return true;
    }
  }

  private async Task<bool> SendNotificationAsync(Rule rule, RuleEvaluationResult evaluation, Subscription subscription, UserLocale locale, CancellationToken cancellationToken)
  {
    var alertText = BuildAlertText(evaluation, rule, locale);

    // Chart the condition that actually fired (the first met one — it leads the alert
    // text), not the subscription's primary indicator.
    var met = evaluation.MetConditions;
    ConditionOutcome? firedOutcome = met.Count > 0 ? met[0] : null;
    var firedCondition = firedOutcome?.Condition;

    // The subscription carries no instrument of its own: the alert identifies the instrument
    // that fired. Without a met condition (a Then-false notification) the rule's first
    // watched instrument gives the context instead.
    var firedTicker = firedCondition?.Ticker ?? rule.When.CollectInstruments(rule.Id).FirstOrDefault()?.Ticker ?? "";
    var firedExchangeName = firedCondition?.ExchangeName;

    // The candle the condition saw (UTC). The trigger chart always renders around it: when
    // the candle cache lags behind (provider outage, slow backfill) the wall clock points
    // past the signal, and every later re-render of the trigger chart would then draw a
    // newer window with the marker moved past the candle that fired.
    var allConditions = evaluation.Conditions;
    var candleAt = firedOutcome?.CandleDate
                   ?? (allConditions.Count > 0 ? allConditions[0].CandleDate : null);

    // Accumulating indicators say nothing new until the candle closes — one notification per
    // (condition, candle) is enough. The check runs from the cooldown-gated pipeline, but the
    // cooldown lives in memory, so the dedup question goes to the database: a restart must
    // not resurrect duplicates of an already-notified candle.
    if (firedCondition is not null && candleAt is not null && SameCandleRepeatIsNoise.Contains(firedCondition.Indicator))
    {
      var previous = await _triggerHistoryRepository.GetLatestByConditionAsync(subscription.Id, firedCondition.Id, cancellationToken);

      if (previous is not null && (previous.TriggeredCandleAt ?? previous.TriggeredAt) == candleAt)
      {
        _logger.Debug("Suppressing duplicate {Indicator} notification for subscription {Id} on candle {Candle}",
          firedCondition.Indicator, subscription.Id.ToString(), candleAt.Value.ToString("O"));
        await _subscriptionRepository.SetCooldownAsync(subscription, cancellationToken);
        return false;
      }
    }

    // The history row records when the detection happened: several triggers anchored to one
    // forming candle (cooldown expiry while the condition stays met) must stay distinguishable
    // in the trigger history.
    var triggeredAt = DateTime.UtcNow;

    var triggerId = Guid.CreateVersion7();

    var imagePng = await RenderTriggerChartAsync(subscription, candleAt ?? triggeredAt, firedCondition, locale, cancellationToken);

    _alertDispatcher.AddAlert(subscription, firedTicker, firedExchangeName, rule.Name, alertText, triggerId, imagePng, triggeredAt);

    var alertEvent = new AlertEvent(
      subscription.Id,
      subscription.UserId,
      firedTicker,
      rule.Name,
      alertText,
      triggeredAt
    );
    await _eventBus.PublishAsync(alertEvent, cancellationToken);

    await _triggerHistoryRepository.AddAsync(
      new TriggerHistory
      {
        Id = triggerId,
        SubscriptionId = subscription.Id,
        TriggerValue = alertText,
        TriggeredAt = triggeredAt,
        TriggeredCandleAt = candleAt,
        TriggeredConditionId = firedCondition?.Id,
        // The alert's own copy of the condition it fired on: every later re-render of this
        // trigger's chart draws from it, so editing the rule afterwards (a rebuilt condition
        // tree, retuned parameters) can neither redraw nor orphan a delivered alert
        FiredConditionData = firedCondition is null ? null : _serializer.Serialize(firedCondition)
      },
      cancellationToken
    );

    _logger.Information("Rule notification collected: {Rule} ({Met}) — {Text}", rule.Name, evaluation.Met, alertText);
    return true;
  }

  /// <summary>
  /// Builds the alert text. When the rule fired, it lists every condition that was met (with its
  /// value) so the recipient can see what triggered it. On rules watching several instruments
  /// every line carries its ticker; on single-instrument rules the ticker would only repeat the
  /// channel line, so the lines stay bare. The rule name is rendered by the channel line prefix,
  /// so the text itself only carries the status and the fired conditions.
  /// </summary>
  private static string BuildAlertText(RuleEvaluationResult evaluation, Rule rule, UserLocale locale)
  {
    var header = AlertTexts.RuleHeader(locale.Language, evaluation.Met);

    var metConditions = evaluation.MetConditions;

    if (!evaluation.Met || metConditions.Count == 0)
      return header;

    var showTicker = rule.When.WatchesMultipleInstruments();
    var lines = new List<string> { header };

    foreach (var met in metConditions)
    {
      var prefix = showTicker
        ? $"{met.Condition.Ticker} {met.Condition.Indicator}"
        : met.Condition.Indicator.ToString();

      lines.Add($"• {prefix}: {met.Value ?? AlertTexts.ConditionMet(locale.Language)}");
    }

    return string.Join('\n', lines);
  }

  private async Task<byte[]?> RenderTriggerChartAsync(
    Subscription subscription,
    DateTime triggeredAt,
    RuleCondition? firedCondition,
    UserLocale locale,
    CancellationToken cancellationToken)
  {
    try
    {
      var chartContext = firedCondition is null
        ? null
        : new TriggerChartContext(
            firedCondition.Indicator,
            firedCondition.TemplateData,
            firedCondition.Ticker,
            firedCondition.ExchangeName
          );

      return await _triggerChartService.RenderPngAsync(subscription, triggeredAt, chartContext, locale, cancellationToken);
    }
    catch (Exception ex)
    {
      // A chart failure must never block alert delivery
      _logger.Warning(ex, "Failed to render trigger chart for subscription {Id}", subscription.Id.ToString());
      return null;
    }
  }

  private async Task PublishRuleEventAsync(
    Rule rule, bool conditionMet, Subscription subscription, string actions, CancellationToken cancellationToken)
  {
    // The event describes the rule outcome; the subscription itself has no instrument, so
    // consumers look at the rule's conditions for that
    await _eventBus.PublishAsync(
      new RuleTriggeredEvent(
        rule.Id,
        rule.Name,
        subscription.Id,
        subscription.UserId,
        conditionMet,
        actions,
        DateTime.UtcNow
      ),
      cancellationToken
    );
  }
}
