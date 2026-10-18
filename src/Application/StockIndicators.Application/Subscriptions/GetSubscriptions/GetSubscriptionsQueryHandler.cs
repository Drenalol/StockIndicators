using System.Data;
using Dapper;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Contracts.Subscriptions;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Subscriptions.GetSubscriptions;

/// <summary>
/// Lists the user's subscriptions with the chip filters of the page applied in SQL:
/// the subscription itself carries only status and LastTriggeredAt, so instrument text,
/// type and exchange run through the RuleInstruments projection joined to the persistent
/// Instruments table, and the indicator filter through RuleIndicators. Rules are loaded
/// (Dapper + jsonb) only for the surviving rows.
/// </summary>
public class GetSubscriptionsQueryHandler : IQueryHandler<GetSubscriptionsQuery, List<SubscriptionDto>>
{
  private readonly ISqlConnectionFactory _sqlConnectionFactory;

  public GetSubscriptionsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

  public async Task<List<SubscriptionDto>> Handle(GetSubscriptionsQuery request, CancellationToken cancellationToken)
  {
    await using var connection = await _sqlConnectionFactory.GetOpenConnectionAsync(cancellationToken);

    var (filter, parameters) = BuildFilter(request);

    var subscriptions = (
      await connection.QueryAsync<SubscriptionDto>(
        $"""
         SELECT s."Id", s."UserId", s."Cooldown", s."Status", s."Created", s."Updated",
                s."LastTriggeredAt", s."LastReadAt", s."RuleGroupId", s."IgnoreExtendedSessions"
         FROM "Subscriptions" s
         {filter}
         """,
        parameters
      )
    ).AsList();

    var ids = subscriptions.Select(subscription => subscription.Id).ToList();

    if (ids.Count > 0)
    {
      var outputs = await connection.QueryAsync<SubscriptionOutputDto>(
        "SELECT \"Id\", \"SubscriptionId\", \"Channel\", \"NotificationTemplateId\", \"PushSubscriptionId\", \"ChatId\", \"IsEnabled\" FROM \"SubscriptionOutputs\" WHERE \"SubscriptionId\" = ANY(@Ids)",
        new { Ids = ids });

      var outputsBySubscription = outputs.GroupBy(output => output.SubscriptionId).ToDictionary(group => group.Key, group => group.ToList());

      var ruleIds = subscriptions
        .Select(subscription => subscription.RuleGroupId)
        .Distinct()
        .ToArray();

      var rules = await RuleDtoLoader.LoadRulesAsync(connection, ruleIds, cancellationToken);

      foreach (var subscription in subscriptions)
      {
        if (outputsBySubscription.TryGetValue(subscription.Id, out var subscriptionOutputs))
          subscription.Outputs = subscriptionOutputs;

        if (rules.TryGetValue(subscription.RuleGroupId, out var rule))
          subscription.Rule = rule;
      }
    }

    return subscriptions;
  }

  private static (string Filter, DynamicParameters Parameters) BuildFilter(GetSubscriptionsQuery request)
  {
    var filters = request.Filters;
    var parameters = new DynamicParameters();

    parameters.Add("UserId", request.UserId);

    var conditions = new List<string> { """s."UserId" = @UserId""" };

    // Status arrives as display names; unknown spellings are dropped instead of failing the list
    if (filters.Statuses is { Count: > 0 } statuses)
    {
      var names = statuses
        .Select(status => Enum.TryParse<SubscriptionStatusDto>(status, true, out var parsed) ? parsed.ToString() : null)
        .Where(name => name is not null)
        .Distinct()
        .ToArray();

      if (names.Length > 0)
      {
        conditions.Add("""s."Status" = ANY(@Statuses)""");
        parameters.Add("Statuses", names);
      }
    }

    // "flat" vs "multi-condition" rules: the repository maintains the leaf count on the rule row
    if (filters.RuleType?.ToLowerInvariant() is "flat" or "multi")
    {
      conditions.Add($"""COALESCE(r."ConditionCount", 0) {filters.RuleType.ToLowerInvariant() switch { "flat" => "<= 1", _ => "> 1" }}""");
    }

    if (filters.TriggeredRecently == true)
    {
      conditions.Add($"""s."LastTriggeredAt" >= now() - make_interval(days => {GetSubscriptionsFilters.TriggeredWindowDays})""");
    }

    // Instrument-backed filters go through the rule->instruments projection; an empty
    // projection exchange matches the ticker on any exchange
    if (!string.IsNullOrWhiteSpace(filters.Type) || !string.IsNullOrWhiteSpace(filters.ExchangeName))
    {
      var instrumentConditions = new List<string>();

      if (!string.IsNullOrWhiteSpace(filters.Type))
      {
        instrumentConditions.Add("""lower(i."Type") = lower(@Type)""");
        parameters.Add("Type", filters.Type.Trim(), DbType.String);
      }

      if (!string.IsNullOrWhiteSpace(filters.ExchangeName))
      {
        instrumentConditions.Add("""lower(i."ExchangeName") = lower(@Exchange)""");
        parameters.Add("Exchange", filters.ExchangeName.Trim(), DbType.String);
      }

      conditions.Add($"""
        EXISTS (
          SELECT 1 FROM "RuleInstruments" ri
          JOIN "Instruments" i ON lower(i."Ticker") = lower(ri."Ticker")
            AND (ri."ExchangeName" = '' OR lower(i."ExchangeName") = lower(ri."ExchangeName"))
          WHERE ri."RuleId" = s."RuleGroupId" AND {string.Join(" AND ", instrumentConditions)}
        )
        """);
    }

    // Free text matches the instruments of the rule (the same FTS as the catalog) or,
    // as before the server-side search, the name the user gave the rule itself
    var search = InstrumentSearchParameters.Build(filters.Search);

    if (search is not null)
    {
      search.AddTo(parameters);

      conditions.Add($"""
        (
          EXISTS (
            SELECT 1 FROM "RuleInstruments" ri
            JOIN "Instruments" i ON lower(i."Ticker") = lower(ri."Ticker")
              AND (ri."ExchangeName" = '' OR lower(i."ExchangeName") = lower(ri."ExchangeName"))
            WHERE ri."RuleId" = s."RuleGroupId" AND {search.Where("i")}
          )
          OR EXISTS (
            SELECT 1 FROM "Rules" rr
            WHERE rr."Id" = s."RuleGroupId" AND rr."Name" ILIKE '%' || @QLike || '%'
          )
        )
        """);
    }

    if (Enum.TryParse<Indicator>(filters.Indicator, true, out var indicator))
    {
      conditions.Add("""EXISTS (SELECT 1 FROM "RuleIndicators" rind WHERE rind."RuleId" = s."RuleGroupId" AND lower(rind."Indicator") = lower(@Indicator))""");
      parameters.Add("Indicator", indicator.ToString(), DbType.String);
    }

    // Joined for the ConditionCount filter only when it is actually used
    var join = filters.RuleType?.ToLowerInvariant() is "flat" or "multi"
      ? "LEFT JOIN \"Rules\" r ON r.\"Id\" = s.\"RuleGroupId\""
      : string.Empty;

    return (string.Join(' ', join, "WHERE " + string.Join(" AND ", conditions)), parameters);
  }
}
