using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class SmaIndicatorCommandHandler : MovingAverageCommandHandlerBase<SmaIndicatorCommand>
{
  public SmaIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MaIndicatorResult> Handle(SmaIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<MaIndicatorTemplate>(request);

    var smaResult = request.Candles
      .Use(indicatorTemplate.CandlePart.ToCandlePart())
      .ToSma(indicatorTemplate.Periods)
      .LastOrDefault();

    if (smaResult?.Sma is not { } sma)
      return await InsufficientDataResultAsync(request, cancellationToken);

    var candle = request.Candles[^1];

    return await CalculateAsync(
      request,
      sma,
      (double)candle.Open,
      (double)candle.Close,
      indicatorTemplate.Deviation,
      cancellationToken
    );
  }
}