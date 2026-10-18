using MediatR;
using Microsoft.Extensions.Options;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Notifications.Channels;
using StockIndicators.Application.Rules.CreateRule;
using StockIndicators.Domain.Models.Indicators;

using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Domain.Models.Telegram;

using StockIndicators.Abstract;

namespace StockIndicators.Application.Subscriptions.CreateSubscription;

public class CreateSubscriptionCommandHandler : ICommandHandler<CreateSubscriptionCommand, Guid>
{
  /// <summary>Hard per-source budget for the instrument existence check.</summary>
  internal static readonly TimeSpan InstrumentSourceBudget = TimeSpan.FromSeconds(5);

  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly ITelegramChatRepository _telegramChatRepository;
  private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
  private readonly INotificationTemplateRepository _templateRepository;
  private readonly IEnumerable<IInstrumentSource> _instrumentSources;
  private readonly IMediator _mediator;
  private readonly IInstrumentCacheReader? _instrumentCache;
  private readonly IOptions<AlertOptions> _alertOptions;

  public CreateSubscriptionCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    ITelegramChatRepository telegramChatRepository,
    IPushSubscriptionRepository pushSubscriptionRepository,
    INotificationTemplateRepository templateRepository,
    IEnumerable<IInstrumentSource> instrumentSources,
    IMediator mediator,
    IOptions<AlertOptions> alertOptions,
    IInstrumentCacheReader? instrumentCache = null
  )
  {
    _subscriptionRepository = subscriptionRepository;
    _telegramChatRepository = telegramChatRepository;
    _pushSubscriptionRepository = pushSubscriptionRepository;
    _templateRepository = templateRepository;
    _instrumentSources = instrumentSources;
    _mediator = mediator;
    _alertOptions = alertOptions;
    _instrumentCache = instrumentCache;
  }

  public async Task<Guid> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
  {
    await ValidateInstrumentAsync(request, cancellationToken);

    var id = Guid.CreateVersion7();
    // An account has at most one linked chat (the repository orders by Created)
    var linkedChat = (await _telegramChatRepository.GetByUserIdAsync(request.UserId, cancellationToken)).FirstOrDefault();

    // The subscription is a pure container of its rule: the rule is created first (and
    // commits its instrument projection), then the subscription row references it
    var ruleGroupId = await _mediator.Send(new CreateRuleCommand(request.UserId, request.Rule.Name, request.Rule.When, request.Rule.ThenTrue, request.Rule.ThenFalse), cancellationToken);

    var subscription = new Subscription
    {
      Id = id,
      UserId = request.UserId,
      Cooldown = request.Cooldown,
      Status = SubscriptionStatus.Active,
      Created = DateTime.UtcNow,
      RuleGroupId = ruleGroupId,
      IgnoreExtendedSessions = request.IgnoreExtendedSessions,
      Outputs = await BuildOutputsAsync(request, id, linkedChat, cancellationToken)
    };

    await _subscriptionRepository.AddAsync(subscription, cancellationToken);
    await _subscriptionRepository.CommitAsync(cancellationToken);

    return subscription.Id;
  }

  private async Task<List<SubscriptionOutput>> BuildOutputsAsync(
    CreateSubscriptionCommand request,
    Guid subscriptionId,
    TelegramChat? linkedChat,
    CancellationToken cancellationToken
  )
  {
    // Explicit outputs win; without them the default is Telegram, addressed to the
    // account's linked chat. A subscription without any output channel would never
    // deliver alerts, so creating one is rejected instead of silently stored.
    if (request.Outputs is not { Count: > 0 })
    {
      if (linkedChat is null)
        throw new ValidationException("A subscription must have at least one output channel", null);

      return [new SubscriptionOutput
        {
          Id = Guid.CreateVersion7(),
          SubscriptionId = subscriptionId,
          Channel = NotificationChannel.Telegram,
          ChatId = linkedChat.ChatId
        }];
    }

    SubscriptionOutputValidation.EnsureWithinChannelLimit(request.Outputs.Count);

    var outputs = new List<SubscriptionOutput>(request.Outputs.Count);

    foreach (var output in request.Outputs)
    {
      SubscriptionOutputValidation.EnsureSupported(output.Channel);

      // Templates are channel-agnostic: the referenced one just has to exist
      if (output.NotificationTemplateId is { } templateId)
        await ValidateTemplateAsync(templateId, cancellationToken);

      switch (output.Channel)
      {
        case NotificationChannel.Telegram:
          outputs.Add(new SubscriptionOutput
          {
            Id = Guid.CreateVersion7(),
            SubscriptionId = subscriptionId,
            Channel = NotificationChannel.Telegram,
            NotificationTemplateId = output.NotificationTemplateId,
            // Every Telegram output addresses the account's linked chat; a requested
            // chat id is ignored (it goes stale when another chat is linked), and an
            // unlinked account gets a null addressee the sender fills in after linking
            ChatId = linkedChat?.ChatId
          });
          break;

        case NotificationChannel.WebPush:
          if (output.PushSubscriptionId is null)
            throw new ValidationException("WebPush output requires PushSubscriptionId", null);

          var pushSubscription = await _pushSubscriptionRepository.GetByIdAsync(output.PushSubscriptionId.Value, request.UserId, cancellationToken);

          if (pushSubscription is null || pushSubscription.Platform != PushPlatform.Web)
            throw new ValidationException("Push subscription not found for this account", null);

          outputs.Add(new SubscriptionOutput
          {
            Id = Guid.CreateVersion7(),
            SubscriptionId = subscriptionId,
            Channel = NotificationChannel.WebPush,
            NotificationTemplateId = output.NotificationTemplateId,
            PushSubscriptionId = output.PushSubscriptionId
          });
          break;

        default:
          // Webhook/Discord (URL) and WhatsApp (E.164 phone): destination in Target
          outputs.Add(new SubscriptionOutput
          {
            Id = Guid.CreateVersion7(),
            SubscriptionId = subscriptionId,
            Channel = output.Channel,
            NotificationTemplateId = output.NotificationTemplateId,
            Target = SubscriptionOutputValidation.NormalizeTarget(output.Channel, output.Target, _alertOptions.Value.AllowPrivateWebhookTargets)
          });
          break;
      }
    }

    SubscriptionOutputValidation.EnsureAtMostOnePerChannel(outputs);

    return outputs;
  }

  /// <summary>
  /// Templates are channel-agnostic (one text serves every sender), so the only
  /// rule on the reference path is that the template exists.
  /// </summary>
  private async Task ValidateTemplateAsync(Guid templateId, CancellationToken cancellationToken)
  {
    var template = await _templateRepository.GetByIdAsync(templateId, cancellationToken);

    if (template is null)
      throw new ValidationException("Notification template not found", null);
  }

  /// <summary>
  /// Strict scoping: a subscription must reference an instrument that actually exists
  /// on the requested exchange, otherwise it could never be activated.
  /// The warm aggregated instruments list (<see cref="IInstrumentCacheReader"/>) is the
  /// authority on the request path — the client selects the instrument from the list the
  /// API serves, so a ticker + exchange match confirms existence without querying any
  /// provider. When the client also sends its resolved identity
  /// (<see cref="CreateSubscriptionCommand.Instrument"/>), a miss in the warm list is a
  /// definitive NotFound. Without an identity (legacy caller) or with a cold cache the
  /// handler falls back to iterating the sources with a hard per-source budget
  /// (<see cref="InstrumentSourceBudget"/>): a slow or dead source is "not owned".
  /// </summary>
  private async Task ValidateInstrumentAsync(CreateSubscriptionCommand request, CancellationToken cancellationToken)
  {
    if (request.ExchangeName == null)
      return;

    if (_instrumentCache is { IsWarm: true } cache)
    {
      // The aggregated list is the authority: a match by ticker + exchange (any provider)
      // is sufficient — the client selects the instrument from that very list. A warm list
      // without the pair is authoritative: the instrument does not exist, so a fake or
      // stale identity must not fall through to the slow provider iteration.
      var cached = await cache.TryGetAsync(request.Ticker, request.Instrument?.Source, cancellationToken)
                   ?? await cache.TryGetAsync(request.Ticker, null, cancellationToken);

      if (cached != null && (cached.ExchangeName ?? "") == (request.ExchangeName ?? ""))
        return;

      if (request.Instrument is not null)
        throw new InstrumentNotFoundException(request.Ticker);
    }

    // No identity (legacy API caller) or a cold cache: iterate the sources with a hard
    // per-source budget — a slow or dead source is treated as "not owned".
    foreach (var source in _instrumentSources)
    {
      try
      {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(InstrumentSourceBudget);

        var instrument = await source.GetInstrumentAsync(request.Ticker, cts.Token);

        if (string.Equals(instrument.ExchangeName, request.ExchangeName, StringComparison.OrdinalIgnoreCase))
          return;
      }
      catch
      {
        // instrument not owned by this source, or the source is too slow — try the next one
      }
    }

    throw new InstrumentNotFoundException(request.Ticker);
  }
}
