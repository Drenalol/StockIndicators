using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;
using DomainTimeFrame = StockIndicators.Domain.Models.Candles.TimeFrame;
using ContractsTimeFrame = StockIndicators.Contracts.Candles.TimeFrame;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Mapping between the API rule DTOs and the domain rule model.
/// </summary>
public static class RuleMappers
{
  public static Rule ToDomain(Guid id, string name, RuleGroupDto when, RuleActionDto[]? thenTrue, RuleActionDto[]? thenFalse)
  {
    return new Rule
    {
      Id = id,
      Name = name,
      When = GroupToDomain(when, id),
      ThenTrue = (thenTrue ?? []).Select(ActionToDomain).ToList(),
      ThenFalse = (thenFalse ?? []).Select(ActionToDomain).ToList()
    };
  }

  public static RuleDataPayload ToPayload(Rule rule)
  {
    return new RuleDataPayload
    {
      When = rule.When,
      ThenTrue = [.. rule.ThenTrue],
      ThenFalse = [.. rule.ThenFalse]
    };
  }

  public static RuleDto ToDto(Rule rule)
  {
    return new RuleDto(
      rule.Id,
      rule.Name,
      GroupToDto(rule.When),
      [.. rule.ThenTrue.Select(ActionToDto)],
      [.. rule.ThenFalse.Select(ActionToDto)]
    );
  }

  public static RuleGroup GroupToDomain(RuleGroupDto dto, Guid? idOverride = null)
  {
    var group = new RuleGroup
    {
      Id = idOverride ?? dto.Id ?? Guid.CreateVersion7(),
      LogicalOperator = Map(dto.Operator)
    };

    group.Conditions = dto.Conditions?.Select(ConditionToDomain).ToList() ?? [];
    group.Groups = (dto.Groups ?? []).Select(sub => GroupToDomain(sub)).ToList();

    return group;
  }

  public static RuleGroupDto GroupToDto(RuleGroup group)
  {
    return new RuleGroupDto(
      group.Id,
      Map(group.LogicalOperator),
      [.. group.Conditions.Select(ConditionToDto)],
      [.. group.Groups.Select(GroupToDto)]
    );
  }

  public static RuleCondition ConditionToDomain(RuleConditionDto dto)
  {
    return new RuleCondition
    {
      Id = dto.Id ?? Guid.CreateVersion7(),
      Indicator = Enum.Parse<Indicator>(dto.Indicator),
      TemplateData = dto.TemplateData?.ToString(Formatting.None),
      Operator = Enum.Parse<ConditionOperator>(dto.Operator.ToString()),
      ReferenceType = Enum.Parse<ReferenceType>(dto.ReferenceType.ToString()),
      ReferenceIndicator = dto.ReferenceIndicator is { Length: > 0 } ? Enum.Parse<Indicator>(dto.ReferenceIndicator) : null,
      ReferenceTemplateData = dto.ReferenceTemplateData?.ToString(Formatting.None),
      ReferenceCandlePart = dto.ReferenceCandlePart is { } part ? (CandlePart)Enum.Parse<CandlePart>(part.ToString()) : null,
      ReferenceValue = dto.ReferenceValue,
      Deviation = dto.Deviation,
      Ticker = dto.Ticker,
      ExchangeName = dto.ExchangeName,
      TimeFrame = (DomainTimeFrame)(int)dto.TimeFrame
    };
  }

  public static RuleConditionDto ConditionToDto(RuleCondition condition)
  {
    return new RuleConditionDto(
      condition.Id,
      condition.Indicator.ToString(),
      condition.TemplateData is { Length: > 0 } ? JToken.Parse(condition.TemplateData) : null,
      (ConditionOperatorDto)Enum.Parse<ConditionOperatorDto>(condition.Operator.ToString()),
      (ReferenceTypeDto)Enum.Parse<ReferenceTypeDto>(condition.ReferenceType.ToString()),
      condition.ReferenceIndicator?.ToString(),
      condition.ReferenceTemplateData is { Length: > 0 } ? JToken.Parse(condition.ReferenceTemplateData) : null,
      condition.ReferenceCandlePart is { } part ? (CandlePartDto)Enum.Parse<CandlePartDto>(part.ToString()) : null,
      condition.ReferenceValue,
      condition.Deviation,
      condition.Ticker,
      condition.ExchangeName,
      (ContractsTimeFrame)(int)condition.TimeFrame
    );
  }

  public static RuleAction ActionToDomain(RuleActionDto dto)
  {
    return new RuleAction
    {
      Id = dto.Id ?? Guid.CreateVersion7(),
      Type = (ActionType)Enum.Parse<ActionType>(dto.Type.ToString()),
      NotificationTemplateId = dto.NotificationTemplateId,
      Parameters = dto.Parameters?.ToString(Formatting.None)
    };
  }

  public static RuleActionDto ActionToDto(RuleAction action)
  {
    return new RuleActionDto(
      action.Id,
      (ActionTypeDto)Enum.Parse<ActionTypeDto>(action.Type.ToString()),
      action.NotificationTemplateId,
      action.Parameters is { Length: > 0 } ? JToken.Parse(action.Parameters) : null
    );
  }

  /// <summary>
  /// Collects the ids of every leaf condition in a rule tree - the keys of the per-condition
  /// indicator state (and persisted trailing-stop anchors), used by the rule write paths to
  /// drop the state of conditions that no longer exist after a rebuild.
  /// </summary>
  public static void CollectConditionIds(RuleGroup? group, ICollection<Guid> into)
  {
    if (group == null)
      return;

    foreach (var condition in group.Conditions ?? [])
      into.Add(condition.Id);

    foreach (var subGroup in group.Groups ?? [])
      CollectConditionIds(subGroup, into);
  }

  private static LogicalOperator Map(LogicalOperatorDto dto) =>
    (LogicalOperator)Enum.Parse<LogicalOperator>(dto.ToString());

  private static LogicalOperatorDto Map(LogicalOperator domain) =>
    (LogicalOperatorDto)Enum.Parse<LogicalOperatorDto>(domain.ToString());
}
