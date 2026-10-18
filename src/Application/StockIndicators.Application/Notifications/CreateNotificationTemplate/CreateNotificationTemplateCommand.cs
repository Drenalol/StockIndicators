using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Notifications.CreateNotificationTemplate;

public record CreateNotificationTemplateCommand(
  Guid UserId,
  string Name,
  string Template
) : ICommand<Guid>;
