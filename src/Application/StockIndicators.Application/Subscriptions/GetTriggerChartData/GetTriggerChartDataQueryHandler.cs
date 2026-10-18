using Serilog;
using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetTriggerChartData;

public class GetTriggerChartDataQueryHandler : IQueryHandler<GetTriggerChartDataQuery, ChartData?>
{
  private readonly ITriggerHistoryRepository _triggerHistoryRepository;
  private readonly ISubscriptionRepository _subscriptionRepository;
  private readonly IRuleRepository _ruleRepository;
  private readonly ITriggerChartService _triggerChartService;
  private readonly IUserLocaleProvider _localeProvider;
  private readonly ISerializer _serializer;
  private readonly ILogger _logger;

  public GetTriggerChartDataQueryHandler(
    ITriggerHistoryRepository triggerHistoryRepository,
    ISubscriptionRepository subscriptionRepository,
    IRuleRepository ruleRepository,
    ITriggerChartService triggerChartService,
    IUserLocaleProvider localeProvider,
    ISerializer serializer,
    ILogger logger
  )
  {
    _triggerHistoryRepository = triggerHistoryRepository;
    _subscriptionRepository = subscriptionRepository;
    _ruleRepository = ruleRepository;
    _triggerChartService = triggerChartService;
    _localeProvider = localeProvider;
    _serializer = serializer;
    _logger = logger.ForContext<GetTriggerChartDataQueryHandler>();
  }

  public async Task<ChartData?> Handle(GetTriggerChartDataQuery request, CancellationToken cancellationToken)
  {
    var trigger = await _triggerHistoryRepository.GetByIdAsync(request.TriggerId, request.SubscriptionId, cancellationToken);

    if (trigger is null)
      return null;

    // Throws SubscriptionNotFoundException (404) when the subscription is missing or belongs to another token
    var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, request.UserId, cancellationToken);

    var resolution = await TriggerChartContextResolver.ResolveAsync(
      _ruleRepository, _serializer, _logger, trigger, subscription, cancellationToken);

    // Nothing to draw at all: the answer is a 404 carrying the stable code, so the client can
    // tell "the condition is gone" from a chart whose candles are missing
    if (!resolution.IsRenderable)
      throw new TriggerChartUnavailableException(resolution.FailureCode!);

    var locale = await _localeProvider.GetAsync(subscription.UserId, cancellationToken);

    return await _triggerChartService.RenderChartDataAsync(subscription, trigger.TriggeredCandleAt ?? trigger.TriggeredAt, resolution.Context, locale, cancellationToken);
  }
}
