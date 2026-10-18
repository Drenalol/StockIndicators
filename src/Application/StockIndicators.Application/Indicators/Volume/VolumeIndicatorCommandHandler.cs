using Serilog;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators.Volume;

/// <summary>
/// A volume spike: the last candle's volume against the average of the previous
/// <see cref="VolumeIndicatorTemplate.Periods"/> candles (the candle's own volume is excluded
/// so a huge spike cannot inflate the average it is compared to). Stateless by design - every
/// candle that reaches the multiple is a signal; subscription cooldown paces the alerts.
/// </summary>
public class VolumeIndicatorCommandHandler : IndicatorCommandHandlerBase<VolumeIndicatorCommand, VolumeIndicatorResult>
{
  public VolumeIndicatorCommandHandler(ILogger logger) : base(logger)
  {
  }

  public override Task<VolumeIndicatorResult> Handle(VolumeIndicatorCommand request, CancellationToken cancellationToken)
  {
    var template = GetTemplate<VolumeIndicatorTemplate>(request);
    var candles = request.Candles;

    if (candles.Length < template.Periods + 1)
      return Task.FromResult(False(new VolumeIndicatorResult()));

    var current = candles[^1];

    decimal sum = 0;

    for (var i = candles.Length - 1 - template.Periods; i < candles.Length - 1; i++)
      sum += candles[i].Volume;

    var average = sum / template.Periods;
    var ratio = average == 0m ? 0d : (double)(current.Volume / average);

    var triggered = average > 0m && (double)current.Volume >= template.Multiplier * (double)average;

    return Task.FromResult(Set(
      new VolumeIndicatorResult { Volume = current.Volume, Average = average, Ratio = ratio },
      triggered));
  }
}
