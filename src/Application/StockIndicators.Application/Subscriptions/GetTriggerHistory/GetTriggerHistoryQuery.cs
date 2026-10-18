using StockIndicators.Application.Configuration.Queries;

namespace StockIndicators.Application.Subscriptions.GetTriggerHistory;

public record GetTriggerHistoryQuery(Guid SubscriptionId, Guid? UserId) : IQuery<IEnumerable<TriggerHistoryDto>>;
