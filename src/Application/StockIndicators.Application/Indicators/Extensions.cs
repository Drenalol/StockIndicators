using FacioQuo.Stock.Indicators;

namespace StockIndicators.Application.Indicators;

public static class Extensions
{
  public static CandlePart ToCandlePart(this Domain.Models.Indicators.CandlePart candlePart) =>
    candlePart switch
    {
      Domain.Models.Indicators.CandlePart.Open => CandlePart.Open,
      Domain.Models.Indicators.CandlePart.High => CandlePart.High,
      Domain.Models.Indicators.CandlePart.Low => CandlePart.Low,
      Domain.Models.Indicators.CandlePart.Close => CandlePart.Close,
      _ => throw new ArgumentOutOfRangeException(nameof(candlePart), candlePart, null)
    };
}