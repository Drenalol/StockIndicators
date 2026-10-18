using Serilog;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.PriceChange;

/// <summary>
/// Percent change between the last close and the close <see cref="PriceChangeIndicatorTemplate.Periods"/>
/// candles back, triggering on the transition into the threshold zone (the RSI state pattern) so a
/// move that stays beyond the threshold does not repeat the alert on every candle.
/// </summary>
public class PriceChangeIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<PriceChangeIndicatorCommand, PriceChangeIndicatorResult>
{
  public PriceChangeIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<PriceChangeIndicatorResult> Handle(PriceChangeIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<PriceChangeIndicatorTemplate>(request);
    var candles = request.Candles;

    var baseIndex = ^(template.Periods + 1);

    if (candles.Length < template.Periods + 1 || candles[baseIndex].Close == 0m)
      return False(new PriceChangeIndicatorResult());

    var change = (double)(candles[^1].Close / candles[baseIndex].Close - 1m) * 100.0;

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new PriceChangeIndicatorResult(), cancellationToken);
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
