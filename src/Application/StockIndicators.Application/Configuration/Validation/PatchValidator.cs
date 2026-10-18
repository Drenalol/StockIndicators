using FluentValidation;
using Microsoft.AspNetCore.JsonPatch;

namespace StockIndicators.Application.Configuration.Validation;

public class PatchValidator : AbstractValidator<JsonPatchDocument>
{
  public PatchValidator()
  {
    RuleFor(patch => patch.Operations).NotEmpty();
    RuleFor(patch => patch.Operations).Custom((operations, context) =>
    {
      if (operations.Any(operation => !JsonPatchValidator.AllowedOperations.Contains(operation.op)))
        context.AddFailure("Operation not allowed");

      if (operations.Any(operation => JsonPatchValidator.RestrictedProperties.Contains(operation.path)))
        context.AddFailure("Denied access to protected property");
    });
  }
}

public static class JsonPatchValidator
{
  public static readonly IReadOnlySet<string> AllowedOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "replace" };
  public static readonly IReadOnlySet<string> RestrictedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/id", "/indicator", "/template", "/template_data", "/isActive", "figi", "notification_template_id", "notification_template" };
}
