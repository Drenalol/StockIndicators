using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.MovingAverage;

public class VwmaIndicatorCommandHandler : MovingAverageCommandHandlerBase<VwmaIndicatorCommand>
{
  public VwmaIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<MaIndicatorResult> Handle(VwmaIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<VwmaIndicatorTemplate>(request);

    var vwmaResult = request.Candles
      .ToVwma(indicatorTemplate.Periods)
      .LastOrDefault();

    if (vwmaResult?.Vwma is not { } vwma)
      return await InsufficientDataResultAsync(request, cancellationToken);

    var candle = request.Candles[^1];

    return await CalculateAsync(
      request,
      vwma,
      (double)candle.Open,
      (double)candle.Close,
      indicatorTemplate.Deviation,
      cancellationToken
    );
  }
}
