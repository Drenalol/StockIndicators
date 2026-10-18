using LazyCache;
using MediatR;
using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Subscriptions.GetSubscription;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetTriggerChart;

/// <summary>
/// Resolves a trigger chart PNG by trigger id for the public web push image endpoint:
/// cache first, then an on-demand re-render from the candle cache.
/// </summary>
public record GetTriggerChartImageQuery(Guid TriggerId) : IQuery<byte[]?>;

public class GetTriggerChartImageQueryHandler : IQueryHandler<GetTriggerChartImageQuery, byte[]?>
{
  private readonly IAppCache _appCache;
  private readonly ITriggerHistoryRepository _triggerHistoryRepository;
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly IRuleRepository _ruleRepository;
  private readonly ITriggerChartService _triggerChartService;
  private readonly IUserLocaleProvider _localeProvider;
  private readonly ISerializer _serializer;
  private readonly ILogger _logger;

  public GetTriggerChartImageQueryHandler(
    IAppCache appCache,
    ITriggerHistoryRepository triggerHistoryRepository,
    ISubscriptionRepository subscriptionRepository,
    IRuleRepository ruleRepository,
    ITriggerChartService triggerChartService,
    IUserLocaleProvider localeProvider,
    ISerializer serializer,
    ILogger logger
  )
  {
    _appCache = appCache;
    _triggerHistoryRepository = triggerHistoryRepository;
    _subscriptionRepository = subscriptionRepository;
    _ruleRepository = ruleRepository;
    _triggerChartService = triggerChartService;
    _localeProvider = localeProvider;
    _serializer = serializer;
    _logger = logger.ForContext<GetTriggerChartImageQueryHandler>();
  }

  public async Task<byte[]?> Handle(GetTriggerChartImageQuery request, CancellationToken cancellationToken)
  {
    if (_appCache.TryGetValue<byte[]>(TriggerChartImages.CacheKey(request.TriggerId), out var cachedImage) && cachedImage is { Length: > 0 })
      return cachedImage;

    var trigger = await _triggerHistoryRepository.GetByIdAsync(request.TriggerId, cancellationToken);

    if (trigger is null)
      return null;

    try
    {
      var subscription = await _subscriptionRepository.GetByIdAsync(trigger.SubscriptionId, null, cancellationToken);

      // The resolver logs why nothing could be drawn; a push image has no client to localize a
      // code for, so this endpoint just answers "no image"
      var resolution = await TriggerChartContextResolver.ResolveAsync(
        _ruleRepository, _serializer, _logger, trigger, subscription, cancellationToken);

      if (!resolution.IsRenderable)
        return null;

      // The cached image (the common case) was rendered at alert time in the owner's
      // timezone; the on-demand re-render must match it.
      var locale = await _localeProvider.GetAsync(subscription.UserId, cancellationToken);

      var png = await _triggerChartService.RenderPngAsync(subscription, trigger.TriggeredCandleAt ?? trigger.TriggeredAt, resolution.Context, locale, cancellationToken);

      if (png is { Length: > 0 })
        _appCache.Add(TriggerChartImages.CacheKey(request.TriggerId), png, DateTimeOffset.UtcNow.Add(TriggerChartImages.CacheTtl));

      return png;
    }
    catch (SubscriptionNotFoundException)
    {
      return null;
    }
  }
}
