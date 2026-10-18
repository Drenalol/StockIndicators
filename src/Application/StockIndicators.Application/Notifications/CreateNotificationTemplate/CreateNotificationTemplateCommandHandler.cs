using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.NotificationTemplates;

namespace StockIndicators.Application.Notifications.CreateNotificationTemplate;

public class CreateNotificationTemplateCommandHandler : ICommandHandler<CreateNotificationTemplateCommand, Guid>
{
  private readonly INotificationTemplateRepository _templateRepository;

  public CreateNotificationTemplateCommandHandler(INotificationTemplateRepository templateRepository) => _templateRepository = templateRepository;

  public async Task<Guid> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
  {
    NotificationTemplateValidation.ValidateName(request.Name);
    NotificationTemplateValidation.ValidateBody(request.Template);

    var template = new NotificationTemplate
    {
      Id = Guid.CreateVersion7(),
      Name = request.Name,
      Template = request.Template,
      IsBundled = false,
      // The creator owns the template: reads and the edit/delete surface are scoped by it
      UserId = request.UserId,
      Created = DateTime.UtcNow
    };

    var created = await _templateRepository.AddAsync(template, cancellationToken);

    return created.Id;
  }
}
