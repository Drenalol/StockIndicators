using MediatR;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetTriggerHistory;

public class GetTriggerHistoryQueryHandler : IQueryHandler<GetTriggerHistoryQuery, IEnumerable<TriggerHistoryDto>>
{
  private readonly ITriggerHistoryRepository _triggerHistoryRepository;

  public GetTriggerHistoryQueryHandler(ITriggerHistoryRepository triggerHistoryRepository)
  {
    _triggerHistoryRepository = triggerHistoryRepository;
  }

  public async Task<IEnumerable<TriggerHistoryDto>> Handle(GetTriggerHistoryQuery request, CancellationToken cancellationToken)
  {
    var entries = await _triggerHistoryRepository.GetLatestBySubscriptionIdAsync(request.SubscriptionId, 100, cancellationToken);

    return entries.Select(e => new TriggerHistoryDto(
      e.Id.ToString(),
      e.TriggerValue,
      e.TriggeredAt,
      e.TriggeredCandleAt,
      [.. e.Deliveries
        .OrderBy(d => d.SentAt)
        .Select(d => new TriggerDeliveryDto(d.Channel.ToString(), d.Status.ToString(), d.Error, d.SentAt))]
    ));
  }
}
