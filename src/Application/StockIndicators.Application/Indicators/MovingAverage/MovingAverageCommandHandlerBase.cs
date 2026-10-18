using Serilog;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Application.Indicators.MovingAverage;

public abstract class MovingAverageCommandHandlerBase<TMaCommand> : StatefulIndicatorCommandHandlerBase<TMaCommand, MaIndicatorResult> where TMaCommand : IndicatorCommandBase<MaIndicatorResult>
{
  protected MovingAverageCommandHandlerBase(IIndicatorCache indicatorCache, ILogger logger) : base(indicatorCache, logger)
  {
  }

  protected async Task<MaIndicatorResult> CalculateAsync(
    TMaCommand command,
    double result,
    double open,
    double close,
    double deviation,
    CancellationToken cancellationToken = default
  )
  {
    var deviationResult = result * deviation;
    var modulo = close - result;
    var indicatorResult = await GetOrAddCacheResultAsync(command, () => new MaIndicatorResult { Result = result }, cancellationToken);
    indicatorResult.Clear();

    return modulo switch
    {
      // Upward cross: the candle opened below the average and closed within the deviation band above it
      >= 0 when modulo <= deviationResult && open <= result && indicatorResult.Test != MaTest.Upward => InternalSet(MaTest.Upward),
      // Downward cross: the candle opened above the average and closed within the deviation band below it
      <= 0 when modulo >= -deviationResult && open >= result && indicatorResult.Test != MaTest.Downward => InternalSet(MaTest.Downward),
      _ => InternalSet(MaTest.None)
    };

    MaIndicatorResult InternalSet(MaTest test)
    {
      indicatorResult.Result = result;
      indicatorResult.Test = test;
      return Set(indicatorResult, test != MaTest.None);
    }
  }

  /// <summary>
  /// Result for the case when the candles do not cover the average's warmup window
  /// (e.g. SMA(50) over fewer than 50 candles): the indicator has no value yet, so the
  /// subscription must not trigger and must not crash on the missing result.
  /// </summary>
  protected async Task<MaIndicatorResult> InsufficientDataResultAsync(TMaCommand command, CancellationToken cancellationToken = default)
  {
    var indicatorResult = await GetOrAddCacheResultAsync(command, () => new MaIndicatorResult(), cancellationToken);
    indicatorResult.Test = MaTest.None;
    indicatorResult.Clear();
    return indicatorResult;
  }
}