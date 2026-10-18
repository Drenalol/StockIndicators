using Dapper;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Subscriptions.GetSubscription;

public class GetSubscriptionQueryHandler : IQueryHandler<GetSubscriptionQuery, SubscriptionDto>
{
  private readonly ISqlConnectionFactory _sqlConnectionFactory;

  public GetSubscriptionQueryHandler(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

  public async Task<SubscriptionDto> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
  {
    await using var connection = await _sqlConnectionFactory.GetOpenConnectionAsync(cancellationToken);

    var subscription = (
      await connection.QueryAsync<SubscriptionDto>(
        "SELECT \"Id\", \"UserId\", \"Cooldown\", \"Status\", \"Created\", \"Updated\", \"LastTriggeredAt\", \"LastReadAt\", \"RuleGroupId\", \"IgnoreExtendedSessions\" FROM \"Subscriptions\" WHERE \"Id\" = @SubscriptionId AND \"UserId\" = @UserId",
        new { request.SubscriptionId, request.UserId }
      )
    ).SingleOrDefault();

    if (subscription == null)
      throw new SubscriptionNotFoundException();

    subscription.Outputs = (
      await connection.QueryAsync<SubscriptionOutputDto>(
        "SELECT \"Id\", \"SubscriptionId\", \"Channel\", \"NotificationTemplateId\", \"PushSubscriptionId\", \"ChatId\", \"Target\", \"IsEnabled\" FROM \"SubscriptionOutputs\" WHERE \"SubscriptionId\" = @SubscriptionId",
        new { SubscriptionId = subscription.Id }
      )).AsList();

    subscription.Rule = await RuleDtoLoader.LoadRuleAsync(connection, subscription.RuleGroupId, cancellationToken);

    return subscription;
  }
}
