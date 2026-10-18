using Serilog;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Gap;

public class GapIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<GapIndicatorCommand, GapIndicatorResult>
{
  public GapIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<GapIndicatorResult> Handle(GapIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new GapIndicatorResult(), cancellationToken);

    var previous = request.Previous;

    if (indicatorResult.Previous == previous.Timestamp)
      return False(indicatorResult);

    indicatorResult.Clear();

    var indicatorTemplate = GetTemplate<GapIndicatorTemplate>(request);

    var current = request.Current;

    // Validate candle order: previous must be strictly before current
    if (previous.Timestamp >= current.Timestamp)
    {
      Logger.Warning(
        "Candles out of order for {Ticker}: previous={PreviousDate} >= current={CurrentDate}, skipping gap calculation",
        request.Ticker, previous.Timestamp, current.Timestamp);
      return False(indicatorResult);
    }

    var gapPercent = GetGapPercent();

    indicatorResult.Previous = previous.Timestamp;
    indicatorResult.Percent = gapPercent;

    // The Direction filter splits the two gap sides; Both keeps the historical behavior
    // (the default for stored rules written before Direction existed).
    // A flat open (open equals the previous close) has no side: only Both accepts it.
    AlertDirection? gapSide = current.Open > previous.Close ? AlertDirection.Up
      : current.Open < previous.Close ? AlertDirection.Down
      : null;

    return Set(indicatorResult, IsGap(gapPercent, indicatorTemplate.Percent) && indicatorTemplate.Direction.Matches(gapSide));

    static bool IsGap(double resultPercent, double templatePercent) => resultPercent >= templatePercent;

    double GetGapPercent()
    {
      decimal low;
      decimal high;

      if (current.Open > previous.Close)
      {
        low = previous.Close;
        high = current.Open;
      }
      else
      {
        low = current.Open;
        high = previous.Close;
      }

      return 100 - decimal.ToDouble(low / high * 100);
    }
  }
}
