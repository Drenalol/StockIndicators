using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Donchian;

public class DonchianIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<DonchianIndicatorCommand, DonchianIndicatorResult>
{
  public DonchianIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<DonchianIndicatorResult> Handle(DonchianIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<DonchianIndicatorTemplate>(request);

    var donchianResult = request.Candles
      .ToDonchian(template.Period)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new DonchianIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    indicatorResult.UpperBand = donchianResult.UpperBand.GetValueOrDefault();
    indicatorResult.MiddleBand = donchianResult.Centerline.GetValueOrDefault();
    indicatorResult.LowerBand = donchianResult.LowerBand.GetValueOrDefault();

    var position = GetPosition(CloseOf(request.Candles[^1]), indicatorResult);
    var prevPosition = GetPosition(CloseOf(request.Candles.Length > 1 ? request.Candles[^2] : request.Candles[^1]), indicatorResult);

    // Trigger when the close breaks the channel extremes or returns inside, mirroring Bollinger Bands.
    var triggered = (prevPosition == ChannelPosition.Inside && position != ChannelPosition.Inside)
      || (prevPosition != ChannelPosition.Inside && position == ChannelPosition.Inside);

    indicatorResult.Position = position;

    return Set(indicatorResult, triggered);

    static double CloseOf(Candle candle) => (double)candle.Close;

    static ChannelPosition GetPosition(double price, DonchianIndicatorResult result)
    {
      if (result.UpperBand > 0 && price > result.UpperBand)
        return ChannelPosition.AboveUpper;
      if (result.LowerBand > 0 && price < result.LowerBand)
        return ChannelPosition.BelowLower;
      return ChannelPosition.Inside;
    }
  }
}
