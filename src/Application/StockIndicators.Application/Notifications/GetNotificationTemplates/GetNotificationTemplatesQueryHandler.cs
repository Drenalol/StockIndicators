using Dapper;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Notifications.GetNotificationTemplates;

public class GetNotificationTemplatesQueryHandler : IQueryHandler<GetNotificationTemplatesQuery, List<NotificationTemplateDto>>
{
  private readonly ISqlConnectionFactory _sqlConnectionFactory;

  public GetNotificationTemplatesQueryHandler(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

  public async Task<List<NotificationTemplateDto>> Handle(GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
  {
    await using var connection = await _sqlConnectionFactory.GetOpenConnectionAsync(cancellationToken);

    // Per-user scoping: bundled and owner-less legacy templates (created before ownership
    // existed) stay visible to everyone; since the create path stamps its owner, every
    // newer template is visible only to its author. CanEdit drives the edit/delete UI:
    // true for the caller's own rows and for owner-less legacy rows (a first write claims them).
    // Templates are channel-agnostic: one template can serve any output channel
    var templates = await connection.QueryAsync<NotificationTemplateDto>(
      """
      SELECT "Id", "Name", "IsBundled", "Template",
             (NOT "IsBundled" AND ("UserId" IS NULL OR "UserId" = @UserId)) AS "CanEdit"
      FROM "NotificationTemplates"
      WHERE "IsBundled" OR "UserId" IS NULL OR "UserId" = @UserId
      ORDER BY "Name"
      """,
      new { UserId = request.UserId });

    return templates.AsList();
  }
}
