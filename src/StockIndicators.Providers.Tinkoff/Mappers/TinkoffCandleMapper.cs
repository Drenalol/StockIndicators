using StockIndicators.Domain;
using StockIndicators.Domain.Models.Candles;
using Tinkoff.InvestApi.V1;
using Candle = Tinkoff.InvestApi.V1.Candle;

namespace StockIndicators.Providers.Tinkoff.Mappers;

public static class TinkoffCandleMapper
{
  public static Domain.Models.Candles.Candle ToDomain(this HistoricCandle historicCandle, string ticker, TimeFrame timeFrame, string? exchangeName = null) =>
    new(
      ticker,
      historicCandle.Time.ToDateTime(),
      timeFrame,
      historicCandle.Open,
      historicCandle.High,
      historicCandle.Low,
      historicCandle.Close,
      historicCandle.Volume,
      isCompleted: historicCandle.IsComplete,
      exchangeName: exchangeName
    );

  public static Domain.Models.Candles.Candle ToDomain(this Candle candle) =>
    new(
      candle.Ticker,
      candle.Time.ToDateTime(),
      TimeFrame.Minute,
      candle.Open,
      candle.High,
      candle.Low,
      candle.Close,
      candle.Volume,
      isCompleted: false,
      exchangeName: "MOEX"
    );
}
