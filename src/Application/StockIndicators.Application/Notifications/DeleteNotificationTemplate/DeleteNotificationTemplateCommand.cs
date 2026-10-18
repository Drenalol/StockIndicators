using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Notifications.DeleteNotificationTemplate;

public record DeleteNotificationTemplateCommand(
  Guid UserId,
  Guid Id
) : ICommand<Unit>;
