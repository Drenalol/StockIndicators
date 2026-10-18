using StockIndicators.Application.Charts;
using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Instruments.GetInstrumentChart;

/// <summary>
/// The plain price preview chart of an instrument: the newest daily candles without any
/// indicator overlay or trigger marker, re-stamped to the viewer's timezone. Same candle
/// pipeline and <see cref="ChartData"/> payload as the subscription charts, so the client
/// draws it with its own chart core.
/// </summary>
public sealed record GetInstrumentChartQuery(string Ticker, string? ExchangeName, Guid UserId) : IQuery<ChartData?>, IHasUserId;
