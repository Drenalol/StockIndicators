using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using LibraryCandlePart = FacioQuo.Stock.Indicators.CandlePart;
using DomainCandlePart = StockIndicators.Domain.Models.Indicators.CandlePart;

namespace StockIndicators.Application.Indicators.BollingerBands;

public class BollingerBandsIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<BollingerBandsIndicatorCommand, BollingerBandsIndicatorResult>
{
  public BollingerBandsIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<BollingerBandsIndicatorResult> Handle(BollingerBandsIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<BollingerBandsIndicatorTemplate>(request);

    var bbResult = request.Candles
      .Use(template.CandlePart.ToCandlePart())
      .ToBollingerBands(template.Period, template.StandardDeviation)
      .Last();

    var indicatorResult = await GetOrAddCacheResultAsync(request, () => new BollingerBandsIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var price = GetPrice(request.Candles[^1], template.CandlePart);
    var prevPrice = request.Candles.Length > 1 ? GetPrice(request.Candles[^2], template.CandlePart) : price;

    indicatorResult.UpperBand = (double)bbResult.UpperBand.GetValueOrDefault();
    indicatorResult.MiddleBand = (double)bbResult.Sma.GetValueOrDefault();
    indicatorResult.LowerBand = (double)bbResult.LowerBand.GetValueOrDefault();
    indicatorResult.PercentB = bbResult.PercentB.GetValueOrDefault();
    indicatorResult.Width = bbResult.Width.GetValueOrDefault();

    var position = GetPosition(price, indicatorResult);
    var prevPosition = GetPosition(prevPrice, indicatorResult);

    var triggered = false;

    // The Direction filter splits the two bands: Up watches the upper band's events
    // (breakout out of Inside and the return back), Down the lower band's, Both keeps
    // the historical behavior (the default for stored rules written before Direction existed).
    if (prevPosition == BbPosition.Inside && position != BbPosition.Inside)
      triggered = template.Direction.Matches(BandSide(position));
    else if (prevPosition != BbPosition.Inside && position == BbPosition.Inside)
      triggered = template.Direction.Matches(BandSide(prevPosition));

    indicatorResult.Position = position;

    return Set(indicatorResult, triggered);

    static double GetPrice(Domain.Models.Candles.Candle candle, DomainCandlePart part) =>
      part switch
      {
        DomainCandlePart.Open => (double)candle.Open,
        DomainCandlePart.High => (double)candle.High,
        DomainCandlePart.Low => (double)candle.Low,
        _ => (double)candle.Close
      };

    static AlertDirection? BandSide(BbPosition position) => position switch
    {
      BbPosition.AboveUpper or BbPosition.TouchingUpper => AlertDirection.Up,
      BbPosition.BelowLower or BbPosition.TouchingLower => AlertDirection.Down,
      _ => null,
    };

    static BbPosition GetPosition(double price, BollingerBandsIndicatorResult result)
    {
      if (price > result.UpperBand)
        return BbPosition.AboveUpper;
      if (price < result.LowerBand)
        return BbPosition.BelowLower;
      if (result.UpperBand > 0 && Math.Abs(price - result.UpperBand) / result.UpperBand < 0.001)
        return BbPosition.TouchingUpper;
      if (result.LowerBand > 0 && Math.Abs(price - result.LowerBand) / result.LowerBand < 0.001)
        return BbPosition.TouchingLower;
      return BbPosition.Inside;
    }
  }
}
