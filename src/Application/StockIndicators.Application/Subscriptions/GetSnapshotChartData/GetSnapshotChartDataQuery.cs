using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Subscriptions.GetSnapshotChartData;

/// <summary>
/// The interactive (v2) snapshot chart of one stored chart row: the row only identifies the
/// (instrument, timeframe) group; the series are computed live from the candle cache, so the
/// interactive view never lags behind the stored SVG (it follows the cache, like a refresh
/// would without spending the quota). Premium-only (402 otherwise); null when the row does
/// not exist, its group has no conditions left in the rule, or the cache holds no candles.
/// </summary>
public sealed record GetSnapshotChartDataQuery(Guid SubscriptionId, Guid SnapshotId, Guid UserId) : IQuery<ChartData?>, IHasUserId;
