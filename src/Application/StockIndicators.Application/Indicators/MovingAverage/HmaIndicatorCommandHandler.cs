using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class HmaIndicatorCommandHandler : MovingAverageCommandHandlerBase<HmaIndicatorCommand>
{
  public HmaIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MaIndicatorResult> Handle(HmaIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<HmaIndicatorTemplate>(request);

    var hmaResult = request.Candles
      .Use(indicatorTemplate.CandlePart.ToCandlePart())
      .ToHma(indicatorTemplate.Periods)
      .LastOrDefault();

    if (hmaResult?.Hma is not { } hma)
      return await InsufficientDataResultAsync(request, cancellationToken);

    var candle = request.Candles[^1];

    return await CalculateAsync(
      request,
      hma,
      (double)candle.Open,
      (double)candle.Close,
      indicatorTemplate.Deviation,
      cancellationToken
    );
  }
}
