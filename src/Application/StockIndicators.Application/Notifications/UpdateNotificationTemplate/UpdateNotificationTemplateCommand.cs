using MediatR;
using StockIndicators.Application.Configuration.Commands;

namespace StockIndicators.Application.Notifications.UpdateNotificationTemplate;

/// <summary>
/// Renames/retextures an owned template. The channel is immutable: outputs already bound to
/// the template validate it against their own channel, so switching it would silently break them.
/// </summary>
public record UpdateNotificationTemplateCommand(
  Guid UserId,
  Guid Id,
  string Name,
  string Template
) : ICommand<Unit>;
