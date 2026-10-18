using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class WmaIndicatorCommandHandler : MovingAverageCommandHandlerBase<WmaIndicatorCommand>
{
  public WmaIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MaIndicatorResult> Handle(WmaIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<WmaIndicatorTemplate>(request);

    var wmaResult = request.Candles
      .Use(indicatorTemplate.CandlePart.ToCandlePart())
      .ToWma(indicatorTemplate.Periods)
      .LastOrDefault();

    if (wmaResult?.Wma is not { } wma)
      return await InsufficientDataResultAsync(request, cancellationToken);

    var candle = request.Candles[^1];

    return await CalculateAsync(
      request,
      wma,
      (double)candle.Open,
      (double)candle.Close,
      indicatorTemplate.Deviation,
      cancellationToken
    );
  }
}
