using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class KamaIndicatorCommandHandler : MovingAverageCommandHandlerBase<KamaIndicatorCommand>
{
  public KamaIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MaIndicatorResult> Handle(KamaIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<KamaIndicatorTemplate>(request);

    var kamaResult = request.Candles
      .Use(indicatorTemplate.CandlePart.ToCandlePart())
      .ToKama(indicatorTemplate.ErPeriods, indicatorTemplate.FastPeriods, indicatorTemplate.SlowPeriods)
      .LastOrDefault();

    if (kamaResult?.Kama is not { } kama)
      return await InsufficientDataResultAsync(request, cancellationToken);

    var candle = request.Candles[^1];

    return await CalculateAsync(
      request,
      kama,
      (double)candle.Open,
      (double)candle.Close,
      indicatorTemplate.Deviation,
      cancellationToken
    );
  }
}
