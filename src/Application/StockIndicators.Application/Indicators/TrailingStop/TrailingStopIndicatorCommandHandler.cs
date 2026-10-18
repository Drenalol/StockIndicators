using Serilog;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.TrailingStop;

/// <summary>
/// A trailing stop whose threshold follows the best price since arming: the anchor ratchets
/// with every check (long: running max of highs, short: running min of lows) and the alert
/// fires when the close crosses the stop line through the unsafe side - long stop hit on a
/// drop-through, short stop hit on a rise-through. Crossing back re-arms the latch; repeated
/// candles below the line never re-alert.
/// The anchor is persisted (read-through on a cold in-memory state, write-through when it
/// moves), so an API restart does not silently re-anchor the stop at the current price; the
/// anchor of a condition is removed when the condition leaves the rule tree.
/// </summary>
public class TrailingStopIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<TrailingStopIndicatorCommand, TrailingStopIndicatorResult>
{
  private readonly ITrailingStopAnchorStore? _anchorStore;

  public TrailingStopIndicatorCommandHandler(
    IIndicatorCache indicatorCache,
    ILogger logger,
    ITrailingStopAnchorStore? anchorStore = null) : base(indicatorCache, logger) => _anchorStore = anchorStore;

  public override async Task<TrailingStopIndicatorResult> Handle(TrailingStopIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<TrailingStopIndicatorTemplate>(request);
    var candles = request.Candles;

    if (candles.Length == 0)
      return False(new TrailingStopIndicatorResult());

    var last = candles[^1];
    var isLong = template.Type == ChandelierDirection.Long;
    var lastExtreme = isLong ? (double)last.High : (double)last.Low;

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new TrailingStopIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var coldStart = indicatorResult.Anchor is null;
    var persisted = coldStart ? await TryGetAnchorAsync(request.SubscriptionId, cancellationToken) : indicatorResult.Anchor;

    var anchor = persisted is { } persistedAnchor
      ? isLong ? Math.Max(persistedAnchor, lastExtreme) : Math.Min(persistedAnchor, lastExtreme)
      : lastExtreme;

    if (coldStart || indicatorResult.Anchor != anchor)
    {
      indicatorResult.Anchor = anchor;
      await TrySetAnchorAsync(request.SubscriptionId, anchor, cancellationToken);
    }

    indicatorResult.LongPosition = isLong;
    indicatorResult.Line = template.ThresholdType == TrailingThresholdType.Percent
      ? isLong ? anchor * (1 - template.Value / 100) : anchor * (1 + template.Value / 100)
      : isLong ? anchor - template.Value : anchor + template.Value;

    var close = (double)last.Close;
    var previousSide = indicatorResult.Side;

    indicatorResult.Side = close >= indicatorResult.Line ? TrailingSide.Above : TrailingSide.Below;
    indicatorResult.Change = anchor > 0 ? (close / anchor - 1) * 100 : 0;

    // Only the crossing into the unsafe side is the exit signal; crossing back re-arms.
    var triggered = isLong
      ? previousSide == TrailingSide.Above && indicatorResult.Side == TrailingSide.Below
      : previousSide == TrailingSide.Below && indicatorResult.Side == TrailingSide.Above;

    return Set(indicatorResult, triggered);
  }

  // The store is best-effort durability: a database hiccup must never break evaluation, the
  // anchor just lives only in memory until the next successful write.
  private async Task<double?> TryGetAnchorAsync(Guid conditionId, CancellationToken cancellationToken)
  {
    if (_anchorStore is null)
      return null;

    try
    {
      return await _anchorStore.GetAnchorAsync(conditionId, cancellationToken);
    }
    catch (Exception exception)
    {
      Logger.Warning(exception, "Trailing stop anchor read-through failed for condition {ConditionId}", conditionId.ToString());
      return null;
    }
  }

  private async Task TrySetAnchorAsync(Guid conditionId, double anchor, CancellationToken cancellationToken)
  {
    if (_anchorStore is null)
      return;

    try
    {
      await _anchorStore.SetAnchorAsync(conditionId, anchor, cancellationToken);
    }
    catch (Exception exception)
    {
      Logger.Warning(exception, "Trailing stop anchor write-through failed for condition {ConditionId}", conditionId.ToString());
    }
  }
}
