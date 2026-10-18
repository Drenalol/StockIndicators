using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.JsonPatch;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Rules;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.PatchSubscription;

public class PatchSubscriptionCommandValidator : AbstractValidator<PatchSubscriptionCommand>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly IRuleRepository _ruleRepository;
  private readonly ISerializer _serializer;
  private readonly IValidator<JsonPatchDocument> _jsonPatchValidator = new PatchValidator();

  public PatchSubscriptionCommandValidator(
    ISubscriptionRepository subscriptionRepository,
    IRuleRepository ruleRepository,
    ISerializer serializer
  )
  {
    _subscriptionRepository = subscriptionRepository;
    _ruleRepository = ruleRepository;
    _serializer = serializer;
  }

  public override async Task<ValidationResult> ValidateAsync(ValidationContext<PatchSubscriptionCommand> context, CancellationToken cancellationToken = default)
  {
    var command = context.InstanceToValidate;
    var failures = new List<ValidationFailure>();

    var patchValidateResult = await _jsonPatchValidator.ValidateAsync(command.PatchDocument, cancellationToken);

    if (!patchValidateResult.IsValid)
      return patchValidateResult;

    // The patch is applied to a materialized copy of the stored subscription, so the plan
    // checks validate the state the save would produce
    var subscription = await _subscriptionRepository.GetByIdAsync(command.SubscriptionId, command.UserId, cancellationToken);
    var originalRuleGroup = subscription.RuleGroupId;

    // Outputs are reconciled by the handler outside JSON Patch; children would break ApplyTo
    command.PatchDocument.Operations.RemoveAll(operation => IsTopLevelPath(operation.path, "Outputs"));

    // Status is not a client-editable field: turning a subscription on and off goes through
    // /active, where the watch bookkeeping is kept in sync
    if (command.PatchDocument.Operations.Any(operation => IsTopLevelPath(operation.path, nameof(Subscription.Status))))
    {
      failures.Add(new ValidationFailure("Status", "Subscription status is not patchable"));

      return new ValidationResult(failures);
    }

    // Apps shipped before the Interval-to-Cooldown rename still patch the old property
    // names; rewrite those paths before ApplyTo resolves them against the renamed model
    foreach (var operation in command.PatchDocument.Operations)
      operation.path = RewriteLegacyCooldownPath(operation.path);

    command.PatchDocument.ApplyTo(subscription, error => failures.Add(new ValidationFailure("PatchDocument", error.ErrorMessage)));

    if (failures.Count > 0)
      return new ValidationResult(failures);

    // A subscription stores no indicator/instrument/parameters: those live on the rule and
    // are edited through the rule endpoints; only cooldown/outputs/rule-link are patchable.

    if (subscription.Cooldown < Limits.MinCooldown)
      failures.Add(new ValidationFailure("Cooldown", $"Cooldown must be at least {(int)Limits.MinCooldown.TotalSeconds} seconds"));

    // Re-attaching a rule (the edit flow materializes a rule for an old subscription and
    // links it) must not smuggle in a tree the rule endpoints would never accept today: the
    // referenced rule is re-validated the same way they validate it. The throws below
    // (RuleNotFound/Validation) surface with their regular status codes.
    if (subscription.RuleGroupId != originalRuleGroup && subscription.RuleGroupId is { } attachedRuleId)
    {
      var rule = await _ruleRepository.GetByIdForUserAsync(attachedRuleId, command.UserId, cancellationToken) ?? throw new RuleNotFoundException();

      var when = RuleMappers.GroupToDto(rule.When);
      RuleValidation.Validate(when);
      RuleValidation.ValidateConditions(_serializer, when);
    }

    var baseResult = base.Validate(context);

    if (!baseResult.IsValid)
      return baseResult;

    return failures.Count > 0 ? new ValidationResult(failures) : new ValidationResult();
  }

  // "/IntervalSeconds" (and the legacy TimeSpan form "/Interval") rewritten to the
  // renamed properties; other paths are returned untouched
  private static string RewriteLegacyCooldownPath(string path)
  {
    if (path.Length < 2 || path[0] != '/')
      return path;

    var slash = path.IndexOf('/', 1);
    var segment = slash < 0 ? path[1..] : path[1..slash];

    var renamed = segment.ToLowerInvariant() switch
    {
      "intervalseconds" => "CooldownSeconds",
      "interval" => "Cooldown",
      _ => segment,
    };

    return renamed == segment ? path : "/" + renamed + (slash < 0 ? string.Empty : path[slash..]);
  }

  private static bool IsTopLevelPath(string path, string propertyName)
  {
    var segment = path.TrimStart('/').Split('/')[0];
    return string.Equals(segment, propertyName, StringComparison.OrdinalIgnoreCase);
  }
}
