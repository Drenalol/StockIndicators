using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;

public interface ISubscriptionSnapshotRenderer
{
  /// <summary>
  /// Resolves the subscription's snapshot chart rows, one per (instrument, timeframe) group
  /// of the rule's indicator conditions; groups resolve in parallel. The rows carry the
  /// group identity, the legend names and the freshness anchor - the picture itself is
  /// computed from the live candle cache when a chart is requested. Returns null when the
  /// rule is gone or not a single group has candles left (the cache window expired); a
  /// partial result (some groups empty) returns whatever succeeded, so the page shows the
  /// rest and the failed groups come back on the next refresh.
  /// </summary>
  Task<TriggerSnapshot[]?> RenderAsync(
    Subscription subscription,
    Rule? rule,
    bool showTriggerMarker,
    CancellationToken cancellationToken = default
  );
}

public class SubscriptionSnapshotRenderer : ISubscriptionSnapshotRenderer
{
  private readonly ITriggerChartService _triggerChartService;
  private readonly ISerializer _serializer;
  private readonly ILogger _logger;

  public SubscriptionSnapshotRenderer(ITriggerChartService triggerChartService, ISerializer serializer, ILogger logger)
  {
    _triggerChartService = triggerChartService;
    _serializer = serializer;
    _logger = logger;
  }

  public async Task<TriggerSnapshot[]?> RenderAsync(
    Subscription subscription,
    Rule? rule,
    bool showTriggerMarker,
    CancellationToken cancellationToken = default
  )
  {
    if (rule is null)
      return null;

    var groups = EnumerateLeafConditions(rule.When)
      .GroupBy(condition => new GroupKey(condition.Ticker, condition.ExchangeName ?? string.Empty, condition.TimeFrame))
      .ToList();

    if (groups.Count == 0)
      return [];

    var snapshots = await Task.WhenAll(
      groups.Select(group => RenderGroupAsync(subscription, group, showTriggerMarker, cancellationToken)));

    var rendered = new List<TriggerSnapshot>(snapshots.Length);

    foreach (var snapshot in snapshots)
      if (snapshot is not null)
        rendered.Add(snapshot);

    return rendered.ToArray();
  }

  /// <summary>
  /// Recursively collects every leaf condition of the rule tree (conditions of the group
  /// itself plus conditions of all nested groups).
  /// </summary>
  internal static IEnumerable<RuleCondition> EnumerateLeafConditions(RuleGroup group)
  {
    foreach (var condition in group.Conditions)
      yield return condition;

    foreach (var nested in group.Groups)
      foreach (var condition in EnumerateLeafConditions(nested))
        yield return condition;
  }

  private async Task<TriggerSnapshot?> RenderGroupAsync(
    Subscription subscription,
    IGrouping<GroupKey, RuleCondition> group,
    bool showTriggerMarker,
    CancellationToken cancellationToken
  )
  {
    // The same indicator with the same template body twice renders twice; distinct pairs only
    var conditions = group
      .GroupBy(condition => (condition.Indicator, condition.TemplateData))
      .Select(distinct => distinct.First())
      .ToList();

    var key = group.Key;
    var context = new SnapshotChartContext(
      key.Ticker,
      key.ExchangeName.Length > 0 ? key.ExchangeName : null,
      key.TimeFrame,
      conditions.Select(condition => new TriggerChartContext(condition.Indicator, condition.TemplateData)).ToArray(),
      showTriggerMarker ? subscription.LastTriggeredAt : null);

    // The chart itself is computed from the live candle cache at request time (the chart
    // endpoint); the stored row only carries the group's identity, legend names and the
    // freshness anchor - no picture bytes are kept anymore
    var lastCandleAt = await _triggerChartService.ResolveSnapshotAnchorAsync(subscription, context, cancellationToken);

    if (lastCandleAt is null)
    {
      _logger.Information(
        "Snapshot chart skipped for {SubscriptionId} ({Ticker} {TimeFrame}): no candles in cache",
        subscription.Id.ToString(),
        key.Ticker,
        key.TimeFrame
      );

      return null;
    }

    var names = new List<string>(conditions.Count);

    foreach (var condition in conditions)
      names.Add(FormatName(condition, ResolveTemplate(condition)));

    return new TriggerSnapshot
    {
      Id = Guid.CreateVersion7(),
      SubscriptionId = subscription.Id,
      Ticker = key.Ticker,
      ExchangeName = key.ExchangeName,
      TimeFrame = key.TimeFrame,
      IndicatorNames = string.Join("; ", names),
      LastCandleAt = lastCandleAt,
      RenderedAt = DateTime.UtcNow
    };
  }

  /// <summary>
  /// Resolves the indicator template of the condition; a missing or corrupt body falls back
  /// to the indicator's default template.
  /// </summary>
  private IIndicatorTemplate ResolveTemplate(RuleCondition condition)
  {
    if (string.IsNullOrWhiteSpace(condition.TemplateData))
      return (IIndicatorTemplate)Activator.CreateInstance(condition.Indicator.GetTemplate())!;

    if (_serializer.DeserializeOrNull(condition.TemplateData, condition.Indicator.GetTemplate()) is IIndicatorTemplate template)
      return template;

    _logger.Warning(
      "Snapshot condition template is not deserializable ({Indicator}); falling back to the indicator defaults",
      condition.Indicator
    );

    return (IIndicatorTemplate)Activator.CreateInstance(condition.Indicator.GetTemplate())!;
  }

  /// <summary>
  /// The legend signature of the condition: moving averages carry their period
  /// ("EMA 21"), every other indicator is drawn by name ("RSI").
  /// </summary>
  private static string FormatName(RuleCondition condition, IIndicatorTemplate template) =>
    template is MaIndicatorTemplate ma ? $"{condition.Indicator} {ma.Periods}" : condition.Indicator.ToString();

  private sealed record GroupKey(string Ticker, string ExchangeName, TimeFrame TimeFrame);
}
