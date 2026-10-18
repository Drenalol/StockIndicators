using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Indicators;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using ContractsTimeFrame = StockIndicators.Contracts.Candles.TimeFrame;

namespace StockIndicators.Application.Rules;

public static class RuleValidation
{
  // Max 5 levels of group nesting (root + 5 nested groups): deep enough for any real
  // WHEN-tree, shallow enough that a rule is still readable in the editor and in an alert text.
  public const int MaxGroupDepth = 5;

  public const int MaxRuleNameLength = 50;

  /// <summary>
  /// Total number of leaf conditions allowed across the whole rule tree (root + nested groups).
  /// TradingView's paid multi-condition alerts cap at 5 (AND-only, one symbol); this ceiling is
  /// deliberately far above it. Evaluation cost per condition is sub-millisecond (in-memory
  /// candle cache + Skender math), so the count itself does not drive load - the number of
  /// distinct instruments and timeframes the pipeline has to keep warm does.
  /// </summary>
  public const int MaxTotalConditions = 20;

  // A branch can hold at most one action of each type, so the ceiling is the
  // number of action types. This both caps the count and enforces uniqueness.
  private static readonly int MaxActionsPerBranch = Enum.GetValues<ActionTypeDto>().Length;

  /// <summary>
  /// A rule group must contain at least one leaf condition somewhere in the tree and stay
  /// within a sane nesting depth.
  /// </summary>
  public static void Validate(RuleGroupDto? group, int depth = 0)
  {
    if (group == null)
      throw new ValidationException("Rule group is required", null,
        [new ValidationError("rule.groupRequired", "Rule group is required")]);

    if (depth > MaxGroupDepth)
      throw new ValidationException($"Rule group nesting is too deep (max {MaxGroupDepth + 1} levels)", null,
        [new ValidationError("rule.nestingTooDeep", $"Rule group nesting is too deep (max {MaxGroupDepth + 1} levels)",
          new Dictionary<string, object?> { ["max"] = MaxGroupDepth + 1 })]);

    if (depth == 0)
    {
      var totalConditions = CountLeafConditions(group);

      if (totalConditions > MaxTotalConditions)
      {
        var message = $"A rule can contain at most {MaxTotalConditions} conditions (found {totalConditions})";
        throw new ValidationException(message, null,
          [new ValidationError("rule.conditionsLimit", message,
            new Dictionary<string, object?> { ["max"] = MaxTotalConditions, ["found"] = totalConditions })]);
      }
    }

    var hasLeaf = group.Conditions is { Length: > 0 };

    foreach (var subGroup in group.Groups ?? [])
    {
      Validate(subGroup, depth + 1);
      hasLeaf = true;
    }

    if (!hasLeaf)
      throw new ValidationException("Rule group must contain at least one condition", null,
        [new ValidationError("rule.atLeastOneCondition", "Rule group must contain at least one condition")]);

    foreach (var condition in group.Conditions ?? [])
    {
      if (string.IsNullOrWhiteSpace(condition.Indicator))
        throw new ValidationException("Condition indicator is required", null,
          [new ValidationError("rule.indicatorRequired", "Condition indicator is required")]);

      if (string.IsNullOrWhiteSpace(condition.Ticker))
        throw new ValidationException("Condition instrument (ticker) is required", null,
          [new ValidationError("rule.instrumentRequired", "Condition instrument (ticker) is required")]);
    }
  }

  /// <summary>
  /// Counts every leaf condition in the rule tree (nested groups included).
  /// </summary>
  public static int CountLeafConditions(RuleGroupDto group)
  {
    var total = group.Conditions?.Length ?? 0;

    foreach (var subGroup in group.Groups ?? [])
      total += CountLeafConditions(subGroup);

    return total;
  }

  /// <summary>
  /// Validates the numeric template parameters of every leaf condition, the same way the
  /// flat subscription path does. A zero or negative value would silently break the
  /// indicator (always-true gap, division by zero, empty period), so it is rejected here.
  /// </summary>
  public static void ValidateConditions(ISerializer serializer, RuleGroupDto group)
  {
    foreach (var subGroup in group.Groups ?? [])
      ValidateConditions(serializer, subGroup);

    foreach (var condition in group.Conditions ?? [])
    {
      if (!Enum.TryParse<Indicator>(condition.Indicator, ignoreCase: true, out var indicator))
        continue;

      if (condition.TemplateData is null)
        continue;

      if (serializer.DeserializeOrNull(condition.TemplateData.ToString(), indicator.GetTemplate()) is not IIndicatorTemplate template)
        continue;

      var errors = IndicatorTemplateValidation.GetErrors(template);

      if (errors.Count == 0)
        continue;

      // The English message keeps the "Condition SMA (AAPL): ..." prefix for plain-string
      // consumers; each code additionally carries the indicator and ticker so a client can
      // render "Condition SMA (AAPL): {localized parameter error}" from its own i18n table.
      var codes = errors
        .Select(e => e.WithArgs(new Dictionary<string, object?>(e.Args)
        {
          ["indicator"] = indicator.ToString(),
          ["ticker"] = condition.Ticker
        }))
        .ToList();

      throw new ValidationException($"Condition {indicator} ({condition.Ticker}): {string.Join("; ", errors.Select(e => e.Message))}", null, codes);
    }
  }

  /// <summary>
  /// The template's own <c>TimeFrame</c> is the authoritative evaluation timeframe for
  /// timeframe-dependent indicators (it is what the indicator handler computes on and what
  /// the trigger chart renders), while <c>condition.TimeFrame</c> is only a fallback and an
  /// echo in the rule DTO. Syncing the stored condition with the template on save keeps the
  /// DTO from lying about the timeframe the evaluator actually uses.
  /// </summary>
  public static void NormalizeConditionTimeFrames(ISerializer serializer, RuleGroupDto? group)
  {
    if (group == null)
      return;

    foreach (var subGroup in group.Groups ?? [])
      NormalizeConditionTimeFrames(serializer, subGroup);

    if (group.Conditions is not { Length: > 0 } conditions)
      return;

    for (var i = 0; i < conditions.Length; i++)
    {
      var condition = conditions[i];

      if (!Enum.TryParse<Indicator>(condition.Indicator, ignoreCase: true, out var indicator) || condition.TemplateData is null)
        continue;

      if (serializer.DeserializeOrNull(condition.TemplateData.ToString(), indicator.GetTemplate()) is not ITimeFrameDependentIndicatorTemplate timeFrameDependent)
        continue;

      conditions[i] = condition with { TimeFrame = (ContractsTimeFrame)(int)timeFrameDependent.TimeFrame };
    }
  }

  /// <summary>
  /// The rule name is required and bounded so it stays usable in lists and alerts.
  /// </summary>
  public static void ValidateName(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
      throw new ValidationException("Rule name is required", null,
        [new ValidationError("rule.nameRequired", "Rule name is required")]);

    if (name.Length > MaxRuleNameLength)
      throw new ValidationException($"Rule name must be at most {MaxRuleNameLength} characters", null,
        [new ValidationError("rule.nameTooLong", $"Rule name must be at most {MaxRuleNameLength} characters",
          new Dictionary<string, object?> { ["max"] = MaxRuleNameLength })]);
  }

  /// <summary>
  /// Validates the THEN-actions: each branch (thenTrue / thenFalse) may hold at most
  /// one action of each type, which caps the count at the number of action types.
  /// </summary>
  public static void ValidateActions(RuleActionDto[]? thenTrue, RuleActionDto[]? thenFalse)
  {
    ValidateActionBranch(thenTrue);
    ValidateActionBranch(thenFalse);
  }

  private static void ValidateActionBranch(RuleActionDto[]? actions)
  {
    if (actions is not { Length: > 0 })
      return;

    if (actions.Length > MaxActionsPerBranch)
      throw new ValidationException($"A rule branch can have at most {MaxActionsPerBranch} actions", null,
        [new ValidationError("rule.tooManyActions", $"A rule branch can have at most {MaxActionsPerBranch} actions",
          new Dictionary<string, object?> { ["max"] = MaxActionsPerBranch })]);

    var seen = new HashSet<ActionTypeDto>();
    foreach (var action in actions)
    {
      if (!seen.Add(action.Type))
        throw new ValidationException($"Duplicate action of type {action.Type} in a rule branch", null,
          [new ValidationError("rule.duplicateAction", $"Duplicate action of type {action.Type} in a rule branch",
            new Dictionary<string, object?> { ["type"] = action.Type.ToString() })]);
    }
  }
}
