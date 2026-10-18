using MediatR;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Domain.Models.NotificationTemplates;

namespace StockIndicators.Application.Notifications.UpdateNotificationTemplate;

public class UpdateNotificationTemplateCommandHandler : ICommandHandler<UpdateNotificationTemplateCommand, Unit>
{
  private readonly INotificationTemplateRepository _templateRepository;

  public UpdateNotificationTemplateCommandHandler(INotificationTemplateRepository templateRepository) => _templateRepository = templateRepository;

  public async Task<Unit> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
  {
    NotificationTemplateValidation.ValidateName(request.Name);
    NotificationTemplateValidation.ValidateBody(request.Template);

    // Owned rows plus owner-less legacy rows (which this write claims for the caller);
    // a foreign or bundled row is indistinguishable from a missing one here
    var template = await _templateRepository.GetEditableForUserAsync(request.Id, request.UserId, cancellationToken);

    if (template != null)
    {
      // Claim-on-edit: an unowned legacy row becomes the first editor's property
      template.UserId ??= request.UserId;
      template.Name = request.Name;
      template.Template = request.Template;
      template.Updated = DateTime.UtcNow;

      await _templateRepository.UpdateAsync(template, cancellationToken);

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
