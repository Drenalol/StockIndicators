using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Subscriptions.GetTriggerChartData;

/// <summary>
/// The interactive (v2) trigger chart: the same picture as the SVG endpoint, returned as raw
/// series data the client plots with its own charting library. Premium-only (402 otherwise);
/// null when the trigger does not exist for the subscription or there is not enough candle data.
/// </summary>
public sealed record GetTriggerChartDataQuery(Guid SubscriptionId, Guid TriggerId, Guid UserId) : IQuery<ChartData?>, IHasUserId;
