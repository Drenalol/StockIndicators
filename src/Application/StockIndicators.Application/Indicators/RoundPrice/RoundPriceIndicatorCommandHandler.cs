using Serilog;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.RoundPrice;

public class RoundPriceIndicatorCommandHandler : IndicatorCommandHandlerBase<RoundPriceIndicatorCommand, IndicatorResult>
{
  public RoundPriceIndicatorCommandHandler(ILogger logger) : base(logger)
  {
  }

  public override Task<IndicatorResult> Handle(RoundPriceIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<RoundPriceIndicatorTemplate>(request);
    // The last candle is the one the rule evaluates (the cache delivers the series ascending,
    // the oldest candle of the window would make the indicator blind to the current price)
    var candle = request.Candles[^1];

    var price = candle.Close < decimal.One
      ? decimal.Round(candle.Close, 3)
      : candle.Close;

    var modulo = price % indicatorTemplate.Accuracy;
    var result = modulo == decimal.Zero;

    return Task.FromResult(result ? True() : False());
  }
}
