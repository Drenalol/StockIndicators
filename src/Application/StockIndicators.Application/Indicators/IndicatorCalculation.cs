/*using System.Collections.Immutable;
using StockIndicators.Domain.Candles;
using StockIndicators.Domain.Indicators;

namespace StockIndicators.Application.Indicators;

/// <summary>
/// Copyright © Dave Skender (https://github.com/DaveSkender/Stock.Indicators)
/// </summary>
public class IndicatorCalculation
{
  private record struct Data(DateTime DateTime, decimal Value);

  private static Data[] GetData(IEnumerable<Candle> candles, CandlePart candlePart) =>
    candles
      .Select(candle => new Data(candle.Date, Value: candle.GetPriceByPart(candlePart)))
      .ToArray();

  public static decimal GetEma(IEnumerable<Candle> candles, int periods, CandlePart candlePart)
  {
    var data = GetData(candles, candlePart);

    var length = data.Length;

    var k = 2M / (period + 1);
    decimal lastEma = 0;

    for (var i = 0; i < period; i++)
      lastEma += data[i].Value;

    lastEma /= period;

    for (var i = 0; i < length; i++)
    {
      var h = data[i];
      var index = i + 1;

      if (index <= period)
        continue;

      var ema = lastEma + k * (h.Value - lastEma);
      lastEma = ema;
    }

    return lastEma;
  }

  public static decimal GetSma(IEnumerable<Candle> candles, int periods, CandlePart candlePart)
  {
    var data = GetData(candles, candlePart);

    return data.Sum(d => d.Value) / period;
  }

  public static StochRsiResult GetStochRsi(
    IEnumerable<Candle> candles,
    int rsiPeriods,
    int stochPeriods,
    int signalPeriods,
    int smoothPeriods
  )
  {
    var data = GetData(candles, CandlePart.Close);
    var rsiResults = GetRsi(data, rsiPeriods);
    var stochResults = GetStoch(rsiResults, stochPeriods, signalPeriods, smoothPeriods);

    return new StochRsiResult();
  }

  public static decimal GetRsi(IEnumerable<Candle> candles, int periods)
  {
    var data = GetData(candles, CandlePart.Close);

    return GetRsi(data, period)[^1].Value;
  }

  private record struct RsiResult(decimal Value);

  private static IReadOnlyList<RsiResult> GetRsi(IReadOnlyList<Data> data, int periods)
  {
    var length = data.Count;
    decimal avgGain = 0;
    decimal avgLoss = 0;

    if (length == 0)
      return ImmutableList<RsiResult>.Empty;

    List<RsiResult> results = new(length);
    var gain = new decimal[length];
    var loss = new decimal[length];

    var lastValue = data[0].Value;

    for (var i = 0; i < data.Count; i++)
    {
      var h = data[i];
      var index = i + 1;

      var result = new RsiResult();
      results.Add(result);

      gain[i] = h.Value > lastValue ? h.Value - lastValue : 0;
      loss[i] = h.Value < lastValue ? lastValue - h.Value : 0;
      lastValue = h.Value;

      if (index > period + 1)
      {
        avgGain = (avgGain * (period - 1) + gain[i]) / period;
        avgLoss = (avgLoss * (period - 1) + loss[i]) / period;

        result.Value = GetResultIfAvgLossGreaterZero();
      }
      else if (index == period + 1)
      {
        decimal sumGain = 0;
        decimal sumLoss = 0;

        for (var p = 1; p <= period; p++)
        {
          sumGain += gain[p];
          sumLoss += loss[p];
        }

        avgGain = sumGain / period;
        avgLoss = sumLoss / period;

        result.Value = GetResultIfAvgLossGreaterZero();
      }
    }

    return results;

    decimal GetResultIfAvgLossGreaterZero() =>
      avgLoss > 0
        ? 100 - 100 / (1 + avgGain / avgLoss)
        : 100;
  }

  public record struct StochResult(decimal Oscillator, decimal Signal, decimal Percent);

  public static StochResult GetStoch(
    IEnumerable<Candle> candles,
    int periods,
    int signalPeriods,
    int smoothPeriods,
    decimal kFactor,
    decimal dFactor
  )
  {
    return new StochResult();
  }

  private static IReadOnlyList<StochResult> GetStoch(
    IReadOnlyList<RsiResult> rsiResults,
    int stochPeriods,
    int signalPeriods,
    int smoothPeriods
  )
  {
    throw new NotImplementedException();
  }

  private static StochResult GetStoch(
    IEnumerable<Data> candles,
    int periods,
    int signalPeriods,
    int smoothPeriods,
    decimal kFactor = 3,
    decimal dFactor = 2
  )
  {
    return new StochResult();
  }
}

public record struct StochRsiResult(decimal Signal, decimal StochRsi);*/