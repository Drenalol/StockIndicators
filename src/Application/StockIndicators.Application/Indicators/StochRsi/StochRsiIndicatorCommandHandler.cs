using Serilog;
using FacioQuo.Stock.Indicators;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.StochRsi;

public class StochRsiIndicatorCommandHandler : StatefulIndicatorCommandHandlerBase<StochRsiIndicatorCommand, StochRsiIndicatorResult>
{
  public StochRsiIndicatorCommandHandler(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  public override async Task<StochRsiIndicatorResult> Handle(StochRsiIndicatorCommand request, CancellationToken cancellationToken)
  {
    var indicatorTemplate = GetTemplate<StochRsiIndicatorTemplate>(request);

    var stochRsiResult = request.Candles.ToStochRsi(
        indicatorTemplate.RsiPeriod,
        indicatorTemplate.StochPeriod,
        indicatorTemplate.SignalPeriod,
        indicatorTemplate.SmoothPeriod
      )
      .Last();

    return await CalculateAsync(request, indicatorTemplate, stochRsiResult, cancellationToken);
  }

  private async Task<StochRsiIndicatorResult> CalculateAsync(
    StochRsiIndicatorCommand command,
    StochRsiIndicatorTemplate template,
    StochRsiResult stochRsiResult,
    CancellationToken cancellationToken = default
  )
  {
    var indicatorResult = await GetOrAddCacheResultAsync(command, () => new StochRsiIndicatorResult(), cancellationToken);
    indicatorResult.Clear();

    var percDPrevious = indicatorResult.PercD;
    var percD = indicatorResult.PercD = stochRsiResult.Signal.GetValueOrDefault();
    var percK = stochRsiResult.StochRsi.GetValueOrDefault();
    var percKPrevious = indicatorResult.PercK;

    var delta = percK - percKPrevious;
    var absDelta = Math.Abs(delta);

    // Epsilon check for zero change
    if (absDelta < StochRsiIndicatorResult.Epsilon)
    {
      indicatorResult.Delta = 0;
      indicatorResult.Arrow = StochRsiIndicatorResult.ArrowNeutral;
      indicatorResult.Trend = StochRsiTrend.None;
    }
    else if (delta > 0)
    {
      indicatorResult.Delta = delta;
      indicatorResult.Arrow = StochRsiIndicatorResult.ArrowUp;
      indicatorResult.Trend = StochRsiTrend.Upward;
    }
    else
    {
      indicatorResult.Delta = delta;
      indicatorResult.Arrow = StochRsiIndicatorResult.ArrowDown;
      indicatorResult.Trend = StochRsiTrend.Downward;
    }

    indicatorResult.PercKPrevious = percKPrevious;
    indicatorResult.PercK = percK;

    // Crossover detection: check if %K crossed %D between previous and current candle.
    // A crossover is confirmed when:
    //   - Previous: %K was below %D (within deviation tolerance)
    //   - Current:  %K is above %D (with deviation margin)
    // StochRSI is a very sensitive oscillator, so - as in the classic methodology - only
    // crossings leaving the channels count: a bullish cross must rise out of the lower
    // zone (MinChannel), a bearish one must fall out of the upper (MaxChannel); mid-range
    // crossings are noise. Setting the channels to 0/100 accepts every crossover.
    var trendChanged = false;

    if (HasPreviousState())
    {
      var wasBelowOrAt = percKPrevious <= percDPrevious + percDPrevious * template.Deviation;
      var isNowAbove = percK - percK * template.Deviation > percD;

      var wasAboveOrAt = percKPrevious - percKPrevious * template.Deviation > percDPrevious;
      var isNowBelow = percD - percD * template.Deviation > percK;

      if (wasBelowOrAt && isNowAbove && percKPrevious <= (double)template.MinChannel)
        trendChanged = true;
      else if (wasAboveOrAt && isNowBelow && percKPrevious >= (double)template.MaxChannel)
        trendChanged = true;
    }

    indicatorResult.PercDPrevious = percD;

    return Set(indicatorResult, trendChanged);

    bool HasPreviousState() =>
      percKPrevious != 0 || percDPrevious != 0;
  }
}
