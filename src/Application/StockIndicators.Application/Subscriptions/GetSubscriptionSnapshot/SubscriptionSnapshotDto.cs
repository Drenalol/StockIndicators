using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Subscriptions.GetSubscriptionSnapshot;

/// <summary>
/// The subscription's live chart snapshot: one chart per (instrument, timeframe) group of
/// its indicator conditions. TimeFrame is the enum name ("Minute"/"Day"/...) so the client
/// feeds it straight into its timeframe label lookup. LastCandleAt is the UTC date of the
/// newest candle in the picture: the client compares it with its own clock and warns that
/// the last known state is shown over a closed exchange. Null when the field did not exist
/// yet when the chart was stored (a refresh fills it).
/// </summary>
public sealed record SnapshotChartDto(
  Guid Id,
  string Ticker,
  string? ExchangeName,
  string TimeFrame,
  IReadOnlyList<string> Indicators,
  DateTime? LastCandleAt,
  DateTime RenderedAt
);

/// <summary>
/// The subscription's live chart snapshot. Manual refreshes are unlimited for every plan:
/// the stored rows only describe the (instrument, timeframe) groups, the series themselves
/// are computed from the live candle cache on every chart request.
/// </summary>
public sealed record SubscriptionSnapshotDto(
  Guid SubscriptionId,
  IReadOnlyList<SnapshotChartDto> Charts,
  DateTime? RenderedAt
);

public static class SnapshotDtoMapper
{
  public static SnapshotChartDto ToDto(TriggerSnapshot row) => new(
    row.Id,
    row.Ticker,
    row.ExchangeName.Length > 0 ? row.ExchangeName : null,
    row.TimeFrame.ToString(),
    row.IndicatorNames.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray(),
    row.LastCandleAt,
    row.RenderedAt
  );
}
