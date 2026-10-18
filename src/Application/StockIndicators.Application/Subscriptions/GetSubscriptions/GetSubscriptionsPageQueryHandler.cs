using Dapper;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Application.Subscriptions.GetSubscription;

namespace StockIndicators.Application.Subscriptions.GetSubscriptions;

public class GetSubscriptionsPageQueryHandler : IQueryHandler<GetSubscriptionsPageQuery, PaginatedResult<SubscriptionDto>>
{
  private readonly ISqlConnectionFactory _sqlConnectionFactory;

  public GetSubscriptionsPageQueryHandler(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

  public async Task<PaginatedResult<SubscriptionDto>> Handle(GetSubscriptionsPageQuery request, CancellationToken cancellationToken)
  {
    await using var connection = await _sqlConnectionFactory.GetOpenConnectionAsync(cancellationToken);

    // The ticker filter runs over the rule->instruments projection (the subscription
    // itself stores no instruments); rules are loaded only for the page rows.
    var filter = request.Search is null
      ? string.Empty
      : """
         AND EXISTS (
           SELECT 1 FROM "RuleInstruments" ri
           WHERE ri."RuleId" = s."RuleGroupId" AND ri."Ticker" ILIKE @Pattern
         )
         """;

    var parameters = new
    {
      request.UserId,
      Pattern = request.Search is null ? null : $"%{request.Search}%",
      request.PageSize,
      Offset = (request.Page - 1) * request.PageSize
    };

    var totalCount = await connection.QuerySingleAsync<int>(
      $"""
       SELECT COUNT(*) FROM "Subscriptions" s
       WHERE s."UserId" = @UserId
       {filter}
       """,
      parameters);

    var subscriptions = (
      await connection.QueryAsync<SubscriptionDto>(
        $"""
         SELECT s."Id", s."UserId", s."Cooldown", s."Status", s."Created", s."Updated",
                s."LastTriggeredAt", s."LastReadAt", s."RuleGroupId", s."IgnoreExtendedSessions"
         FROM "Subscriptions" s
         WHERE s."UserId" = @UserId
         {filter}
         ORDER BY s."Created" DESC
         LIMIT @PageSize OFFSET @Offset
         """,
        parameters
      )
    ).AsList();

    var ruleIds = subscriptions
      .Select(subscription => subscription.RuleGroupId)
      .Distinct()
      .ToArray();

    var rules = await RuleDtoLoader.LoadRulesAsync(connection, ruleIds, cancellationToken);

    foreach (var subscription in subscriptions)
      if (rules.TryGetValue(subscription.RuleGroupId, out var rule))
        subscription.Rule = rule;

    return new PaginatedResult<SubscriptionDto>(subscriptions, totalCount, request.Page, request.PageSize);
  }
}
