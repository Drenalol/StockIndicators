using StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.Indicators.Gap;

public class GapIndicatorCommand : IndicatorCommandBase<GapIndicatorResult>
{
  public Candle Previous => Candles[0];
  
  public Candle Current => Candles[1];
}