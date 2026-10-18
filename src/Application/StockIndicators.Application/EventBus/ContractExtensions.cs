using ContractCandles = StockIndicators.Contracts.Candles;
using DomainCandles = StockIndicators.Domain.Models.Candles;

namespace StockIndicators.Application.EventBus;

/// <summary>
/// Mapping helpers between Domain models and Contracts DTOs.
/// </summary>
public static class ContractExtensions
{
  public static ContractCandles.CandleDto ToDto(this DomainCandles.Candle candle) =>
    new(
      candle.Ticker,
      candle.ExchangeName,
      candle.Timestamp,
      (ContractCandles.TimeFrame)(byte)candle.TimeFrame,
      candle.Open,
      candle.High,
      candle.Low,
      candle.Close,
      (long)candle.Volume,
      candle.IsCompleted
    );

  public static ContractCandles.CandleDto[] ToDtos(this IEnumerable<DomainCandles.Candle> candles) =>
    candles.Select(ToDto).ToArray();
}
