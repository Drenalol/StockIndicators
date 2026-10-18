using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Domain.Models.NotificationTemplates;

namespace StockIndicators.Application.Notifications.DeleteNotificationTemplate;

public class DeleteNotificationTemplateCommandHandler : ICommandHandler<DeleteNotificationTemplateCommand, Unit>
{
  private readonly INotificationTemplateRepository _templateRepository;

  public DeleteNotificationTemplateCommandHandler(INotificationTemplateRepository templateRepository) => _templateRepository = templateRepository;

  public async Task<Unit> Handle(DeleteNotificationTemplateCommand request, CancellationToken cancellationToken)
  {
    // Owned rows plus owner-less legacy rows are deletable; a foreign or bundled row is not
    var template = await _templateRepository.GetEditableForUserAsync(request.Id, request.UserId, cancellationToken);

    if (template != null)
    {
      // Outputs referencing the template fall back to the default rendering (FK SetNull)
      await _templateRepository.DeleteAsync(template.Id, cancellationToken);

      return Unit.Value;
    }

    // The second read tells "bundled, read-only" (400) apart from "foreign/missing" (404)
    var any = await _templateRepository.GetByIdAsync(request.Id, cancellationToken);

    if (any is { IsBundled: true })
      throw new ValidationException("Bundled templates are read-only", null,
        [new ValidationError("template.bundledReadOnly", "Bundled templates are read-only")]);

    throw new NotificationTemplateNotFoundException();
  }
}
