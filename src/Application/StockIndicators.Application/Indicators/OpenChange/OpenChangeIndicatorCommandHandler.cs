using Serilog;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.OpenChange;

/// <summary>
/// Percent change between the last candle's close and its own open (the day's open on a daily
/// chart), triggering on the transition into the threshold zone (the same PriceChange latch) so
/// a move that stays beyond the threshold does not repeat the alert on every candle. A new
/// session starts at the fresh open, which naturally re-arms the latch.
/// </summary>
public class OpenChangeIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<OpenChangeIndicatorCommand, OpenChangeIndicatorResult>
{
  public OpenChangeIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<OpenChangeIndicatorResult> Handle(OpenChangeIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<OpenChangeIndicatorTemplate>(request);
    var candles = request.Candles;

    if (candles.Length == 0 || candles[^1].Open == 0m)
      return False(new OpenChangeIndicatorResult());

    var change = (double)(candles[^1].Close / candles[^1].Open - 1m) * 100.0;

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new OpenChangeIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var wasAbove = indicatorResult.Above;

    indicatorResult.Change = change;
    indicatorResult.Above = IsAbove(change, template.Percent, template.Direction);

    return Set(indicatorResult, indicatorResult.Above && !wasAbove);
  }

  private static bool IsAbove(double change, double percent, PriceChangeDirection direction) =>
    direction switch
    {
      PriceChangeDirection.Up => change >= percent,
      PriceChangeDirection.Down => change <= -percent,
      _ => Math.Abs(change) >= percent,
    };
}
