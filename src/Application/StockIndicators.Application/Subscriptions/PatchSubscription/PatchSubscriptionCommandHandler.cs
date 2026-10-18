using MediatR;
using Serilog;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Domain.Models.Telegram;

namespace StockIndicators.Application.Subscriptions.PatchSubscription;

public class PatchSubscriptionCommandHandler : ICommandHandler<PatchSubscriptionCommand, Unit>
{
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ITriggerSnapshotRepository _snapshotRepository;
  private readonly ITelegramChatRepository _telegramChatRepository;
  private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
  private readonly INotificationTemplateRepository _templateRepository;
  private readonly IOptions<AlertOptions> _alertOptions;
  private readonly ILogger _logger;

  public PatchSubscriptionCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    ITriggerSnapshotRepository snapshotRepository,
    ITelegramChatRepository telegramChatRepository,
    IPushSubscriptionRepository pushSubscriptionRepository,
    INotificationTemplateRepository templateRepository,
    IOptions<AlertOptions> alertOptions,
    ILogger logger
  )
  {
    _subscriptionRepository = subscriptionRepository;
    _snapshotRepository = snapshotRepository;
    _telegramChatRepository = telegramChatRepository;
    _pushSubscriptionRepository = pushSubscriptionRepository;
    _templateRepository = templateRepository;
    _alertOptions = alertOptions;
    _logger = logger.ForContext<PatchSubscriptionCommandHandler>();
  }

  public async Task<Unit> Handle(PatchSubscriptionCommand request, CancellationToken cancellationToken)
  {
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);
    var originalRuleGroupId = subscription.RuleGroupId;

    // Outputs are applied through ReconcileOutputsAsync — JSON Patch deserialization would
    // re-attach existing children as new entities (primary key conflicts on SaveChanges)
    var outputOps = request.PatchDocument.Operations.Where(operation => IsOutputsPath(operation.path)).ToList();
    request.PatchDocument.Operations.RemoveAll(operation => IsOutputsPath(operation.path));

    // Indicators, instruments and parameters are not subscription fields (they live on the
    // rule and are edited through the rule endpoints); anything targeting them fails here
    // via the patch validator or ApplyTo
    request.PatchDocument.ApplyTo(subscription);

    if (outputOps.Count > 0)
      await ApplyOutputOpsAsync(subscription.Id, request.UserId, outputOps, cancellationToken);

    subscription.Updated = DateTime.UtcNow;

    await _subscriptionRepository.CommitAsync(cancellationToken);

    // Drop the stale in-memory copy so the check pipeline picks up the new Cooldown immediately
    await _subscriptionRepository.InvalidateAsync(subscription, cancellationToken);

    // A re-attached rule (the edit flow materializes a rule for an old subscription and links
    // it) invalidates the stored snapshot charts; the next visit re-renders them free
    if (subscription.RuleGroupId != originalRuleGroupId)
      await _snapshotRepository.DeleteBySubscriptionIdAsync(subscription.Id, cancellationToken);

    return Unit.Value;
  }

  private static bool IsOutputsPath(string path)
  {
    var segment = path.TrimStart('/').Split('/')[0];
    return string.Equals(segment, "Outputs", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Simulates JSON Patch operations against the current outputs list and persists the result.
  /// Supported: replace /Outputs (full list), add/replace/remove /Outputs/{index|-}.
  /// </summary>
  private async Task ApplyOutputOpsAsync(
    Guid subscriptionId,
    Guid userId,
    List<Operation> ops,
    CancellationToken cancellationToken
  )
  {
    var current = await _subscriptionRepository.GetOutputsAsync(subscriptionId, cancellationToken);
    var list = JArray.Parse(JsonConvert.SerializeObject(current));

    foreach (var op in ops)
    {
      var token = op.value as JToken ?? JToken.Parse(JsonConvert.SerializeObject(op.value));
      var segments = op.path.TrimStart('/').Split('/');

      if (segments.Length == 1)
      {
        if (op.op is "replace" or "add")
        {
          if (token is not JArray array)
            throw new ValidationException("Outputs patch value must be an array", null);
          list = array;
        }
        else
        {
          throw new ValidationException($"Unsupported patch operation '{op.op}' on Outputs", null);
        }
      }
      else
      {
        var index = segments[1] == "-" ? list.Count : int.Parse(segments[1]);

        switch (op.op)
        {
          case "add":
            list.Insert(index, token);
            break;
          case "replace":
            list[index] = token;
            break;
          case "remove":
            list.RemoveAt(index);
            break;
          default:
            throw new ValidationException($"Unsupported patch operation '{op.op}' on Outputs", null);
        }
      }
    }

    var outputs = new List<SubscriptionOutput>(list.Count);

    foreach (var item in list)
      outputs.Add(item.ToObject<SubscriptionOutput>() ?? throw new ValidationException("Outputs patch entries must be objects", null));

    // Removing the last channel would leave the subscription unable to deliver alerts
    if (outputs.Count == 0)
      throw new ValidationException("A subscription must have at least one output channel", null);

    foreach (var output in outputs)
    {
      SubscriptionOutputValidation.EnsureSupported(output.Channel);

      var stored = current.FirstOrDefault(o => o.Id == output.Id);
      var channelChanged = stored is null || stored.Channel != output.Channel;

      // An omitted Target keeps the stored destination when the channel is unchanged;
      // otherwise the destination is re-validated and normalized (URL / E.164 phone)
      output.Target = string.IsNullOrWhiteSpace(output.Target) && !channelChanged && stored is not null
        ? stored.Target
        : SubscriptionOutputValidation.NormalizeTarget(output.Channel, output.Target, _alertOptions.Value.AllowPrivateWebhookTargets);

      if (output.Channel == NotificationChannel.WebPush)
        output.PushSubscriptionId = await ResolvePushSubscriptionAsync(output.PushSubscriptionId, userId, cancellationToken);
      else
        // Chat/push ids are per channel; a switched channel never carries the push
        // subscription over (and a stray id here would trip the same FK)
        output.PushSubscriptionId = null;

      // Templates are channel-agnostic: the referenced one just has to exist
      if (output.NotificationTemplateId is { } templateId)
        await ValidateTemplateAsync(templateId, cancellationToken);
    }

    SubscriptionOutputValidation.EnsureAtMostOnePerChannel(outputs);

    SubscriptionOutputValidation.EnsureWithinChannelLimit(outputs.Count);

    // An account has at most one linked chat and every Telegram output addresses it:
    // a stale chat id (kept after another chat was linked) is rewritten to the current
    // chat. An unlinked account keeps the null addressee the sender fills in after linking.
    var linkedChat = (await _telegramChatRepository.GetByUserIdAsync(userId, cancellationToken)).FirstOrDefault();

    if (linkedChat is not null)
      foreach (var output in outputs.Where(output => output.Channel == NotificationChannel.Telegram))
        output.ChatId = linkedChat.ChatId;

    await _subscriptionRepository.ReconcileOutputsAsync(subscriptionId, outputs, cancellationToken);
  }

  /// <summary>
  /// The patch path cannot trust ids arriving from the client: the referenced push
  /// subscription may have disappeared since the form was opened (disabled in another
  /// tab/device, or removed by the dispatcher when the push service reported the
  /// endpoint as gone). A dangling id would abort the save with a foreign key
  /// violation (500), so it fails as a validation error (400) asking to re-enable
  /// browser notifications. Mirrors the existence check of the create path.
  /// </summary>
  private async Task<Guid> ResolvePushSubscriptionAsync(Guid? pushSubscriptionId, Guid userId, CancellationToken cancellationToken)
  {
    if (pushSubscriptionId is null)
      throw new ValidationException("WebPush output requires an enabled browser push subscription", null);

    var pushSubscription = await _pushSubscriptionRepository.GetByIdAsync(pushSubscriptionId.Value, userId, cancellationToken);

    if (pushSubscription is null || pushSubscription.Platform != PushPlatform.Web)
      throw new ValidationException("The browser push subscription no longer exists - enable browser notifications again", null);

    return pushSubscription.Id;
  }

  /// <summary>
  /// Same template rule as the create path: templates are channel-agnostic, so the
  /// referenced one only has to exist.
  /// </summary>
  private async Task ValidateTemplateAsync(Guid templateId, CancellationToken cancellationToken)
  {
    var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken);

    if (template is null)
      throw new ValidationException("Notification template not found", null);
  }
}
