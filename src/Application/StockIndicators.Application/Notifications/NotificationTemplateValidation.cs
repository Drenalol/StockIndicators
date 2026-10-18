using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Notifications;

/// <summary>
/// Shared write-path validation for user notification templates (create and update).
/// Carries stable template.* codes so the client localizes the message itself.
/// </summary>
public static class NotificationTemplateValidation
{
  public const int MaxNameLength = 50; // matches the varchar(50) column
  public const int MaxTemplateLength = 4000;

  public static void ValidateName(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
      throw new ValidationException("Template name is required", null,
        [new ValidationError("template.nameRequired", "Template name is required")]);

    if (name.Length > MaxNameLength)
      throw new ValidationException($"Template name must be at most {MaxNameLength} characters", null,
        [new ValidationError("template.nameTooLong", $"Template name must be at most {MaxNameLength} characters",
          new Dictionary<string, object?> { ["max"] = MaxNameLength })]);
  }

  public static void ValidateBody(string? template)
  {
    if (string.IsNullOrWhiteSpace(template))
      throw new ValidationException("Template text is required", null,
        [new ValidationError("template.bodyRequired", "Template text is required")]);

    if (template.Length > MaxTemplateLength)
      throw new ValidationException($"Template text must be at most {MaxTemplateLength} characters", null,
        [new ValidationError("template.bodyTooLong", $"Template text must be at most {MaxTemplateLength} characters",
          new Dictionary<string, object?> { ["max"] = MaxTemplateLength })]);
  }
}
