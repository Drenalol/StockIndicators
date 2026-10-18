using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Application.Indicators;

public abstract class IndicatorCommandBase
{
  /// <summary>
  /// Indicator-state cache slot: the leaf condition being evaluated (rule pipeline).
  /// Every condition needs its own stable slot so stateful indicators (cross direction,
  /// gap previous-candle date) do not reset each other.
  /// </summary>
  public Guid SubscriptionId { get; private init; }

  /// <summary>Instrument the candles belong to (logging context).</summary>
  public string Ticker { get; private init; } = null!;

  public IIndicatorTemplate Template { get; private init; } = null!;
  public Candle[] Candles { get; private init; } = null!;

  public static TCommand Create<TCommand>(Guid subscriptionId, string ticker, IIndicatorTemplate template, Candle[] candles) where TCommand : IndicatorCommandBase, new() =>
    new()
    {
      SubscriptionId = subscriptionId,
      Ticker = ticker,
      Template = template,
      Candles = candles
    };
}

public abstract class IndicatorCommandBase<TResult> : IndicatorCommandBase, ICommand<TResult> where TResult : IndicatorResult;