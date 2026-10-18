using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class EmaIndicatorCommandHandler : MovingAverageCommandHandlerBase<EmaIndicatorCommand>
{
  public EmaIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MaIndicatorResult> Handle(EmaIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<EmaIndicatorTemplate>(request);

    var emaResult = request.Candles
      .Use(indicatorTemplate.CandlePart.ToCandlePart())
      .ToEma(indicatorTemplate.Periods)
      .LastOrDefault();

    if (emaResult?.Ema is not { } ema)
      return await InsufficientDataResultAsync(request, cancellationToken);

    var candle = request.Candles[^1];

    return await CalculateAsync(
      request,
      ema,
      (double)candle.Open,
      (double)candle.Close,
      indicatorTemplate.Deviation,
      cancellationToken
    );
  }
}