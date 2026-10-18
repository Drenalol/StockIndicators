using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Keltner;

public class KeltnerIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<KeltnerIndicatorCommand, KeltnerIndicatorResult>
{
  public KeltnerIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<KeltnerIndicatorResult> Handle(KeltnerIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<KeltnerIndicatorTemplate>(request);

    var keltnerResult = request.Candles
      .ToKeltner(template.Period, template.Multiplier, template.AtrPeriod)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new KeltnerIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    indicatorResult.UpperBand = keltnerResult.UpperBand.GetValueOrDefault();
    indicatorResult.MiddleBand = keltnerResult.Centerline.GetValueOrDefault();
    indicatorResult.LowerBand = keltnerResult.LowerBand.GetValueOrDefault();

    var position = GetPosition(CloseOf(request.Candles[^1]), indicatorResult);
    var prevPosition = GetPosition(CloseOf(request.Candles.Length > 1 ? request.Candles[^2] : request.Candles[^1]), indicatorResult);

    // Trigger when the close leaves the channel or comes back into it, mirroring Bollinger Bands.
    var triggered = (prevPosition == ChannelPosition.Inside && position != ChannelPosition.Inside)
      || (prevPosition != ChannelPosition.Inside && position == ChannelPosition.Inside);

    indicatorResult.Position = position;

    return Set(indicatorResult, triggered);

    static double CloseOf(Candle candle) => (double)candle.Close;

    static ChannelPosition GetPosition(double price, KeltnerIndicatorResult result)
    {
      if (price > result.UpperBand)
        return ChannelPosition.AboveUpper;
      if (result.LowerBand > 0 && price < result.LowerBand)
        return ChannelPosition.BelowLower;
      return ChannelPosition.Inside;
    }
  }
}
