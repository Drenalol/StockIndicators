using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Notifications.GetNotificationTemplates;

public record GetNotificationTemplatesQuery(Guid UserId) : IQuery<List<NotificationTemplateDto>>;
