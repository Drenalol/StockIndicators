using System.Diagnostics;
using MediatR;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Configuration.Locale;
using StockIndicators.Application.Configuration.Metrics;
using StockIndicators.Application.Indicators;
using StockIndicators.Application.Indicators.Adx;
using StockIndicators.Application.Indicators.Aroon;
using StockIndicators.Application.Indicators.Atr;
using StockIndicators.Application.Indicators.BollingerBands;
using StockIndicators.Application.Indicators.Cci;
using StockIndicators.Application.Indicators.Chandelier;
using StockIndicators.Application.Indicators.Cmo;
using StockIndicators.Application.Indicators.Donchian;
using StockIndicators.Application.Indicators.Gap;
using StockIndicators.Application.Indicators.Ichimoku;
using StockIndicators.Application.Indicators.Keltner;
using StockIndicators.Application.Indicators.Macd;
using StockIndicators.Application.Indicators.Mfi;
using StockIndicators.Application.Indicators.MovingAverage;
using StockIndicators.Application.Indicators.Obv;
using StockIndicators.Application.Indicators.OpenChange;
using StockIndicators.Application.Indicators.PriceChange;
using StockIndicators.Application.Indicators.Psar;
using StockIndicators.Application.Indicators.RoundPrice;
using StockIndicators.Application.Indicators.Rsi;
using StockIndicators.Application.Indicators.StochRsi;
using StockIndicators.Application.Indicators.Stochastic;
using StockIndicators.Application.Indicators.SuperTrend;
using StockIndicators.Application.Indicators.Tsi;
using StockIndicators.Application.Indicators.TrailingStop;
using StockIndicators.Application.Indicators.Volume;
using StockIndicators.Application.Indicators.Vortex;
using StockIndicators.Application.Indicators.Vwap;
using StockIndicators.Application.Indicators.Week52HighLow;
using StockIndicators.Application.Indicators.WilliamsR;
using StockIndicators.Domain.Extensions;
using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Indicators.Templates;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Application.Rules;

/// <summary>
/// Evaluates single-condition rules using existing indicator handlers.
/// This is the leaf evaluator — AND/OR logic over groups lives in <see cref="CompositeConditionEvaluator"/>.
/// </summary>
public class SimpleConditionEvaluator : IConditionEvaluator
{
  private readonly IMediator _mediator;
  private readonly ICandleRepository _candleRepository;
  private readonly ISerializer _serializer;
  private readonly ITradingSessionGate? _sessionGate;

  public SimpleConditionEvaluator(
    IMediator mediator,
    ICandleRepository candleRepository,
    ISerializer serializer,
    ITradingSessionGate? sessionGate = null)
  {
    _mediator = mediator;
    _candleRepository = candleRepository;
    _serializer = serializer;
    _sessionGate = sessionGate;
  }

  public async Task<ConditionOutcome> EvaluateAsync(RuleCondition condition, UserLocale locale, bool ignoreExtendedSessions = true, CancellationToken cancellationToken = default)
  {
    var (period, timeFrame, template) = GetCandleCountAndTimeFrame(condition);

    var candles = await _candleRepository.GetLastCandlesAsync(condition.Ticker, condition.ExchangeName, period, timeFrame, cancellationToken);

    if (candles.Length == 0)
      return new ConditionOutcome(condition, false, null);

    // Candle dates in the cache are UTC; pin the kind explicitly so the value can be
    // persisted to the timestamptz trigger history and compared with cache keys later.
    var candleDate = DateTime.SpecifyKind(candles[^1].Timestamp, DateTimeKind.Utc);

    // Post-/pre-market candles keep flowing into the cache and the charts, but by default a
    // condition evaluated on them is silently unmet: most users expect alerts on
    // regular-session data only. A subscription that opted in (IgnoreExtendedSessions=false)
    // evaluates and alerts on extended candles as well.
    if (ignoreExtendedSessions && _sessionGate is not null &&
        _sessionGate.IsExtendedSession(condition.ExchangeName, candles[^1].TimeFrame, candles[^1].Timestamp))
      return new ConditionOutcome(condition, false, null, candleDate);

    // The indicator needs its warmup window (e.g. SMA(50) over 50 candles) before it can
    // produce a meaningful value. With fewer candles the condition is silently skipped:
    // no evaluation, no alert.
    if (template is ICandleCountDependentIndicatorTemplate candleCountDependent && candles.Length < candleCountDependent.GetCandleCount())
      return new ConditionOutcome(condition, false, null, candleDate);

    var evaluationStopwatch = Stopwatch.StartNew();
    var indicatorResult = await EvaluateIndicatorAsync(candles, condition, cancellationToken);
    BusinessMetrics.ConditionEvaluated(condition.Indicator, indicatorResult.IsTriggered, evaluationStopwatch.Elapsed.TotalSeconds);

    return new ConditionOutcome(condition, indicatorResult.IsTriggered, IndicatorResultText.Build(indicatorResult, locale), candleDate);
  }

  private (int Period, TimeFrame TimeFrame, object? Template) GetCandleCountAndTimeFrame(RuleCondition condition)
  {
    var candleCount = 100;
    var timeFrame = condition.TimeFrame;

    if (condition.TemplateData is not { Length: > 0 } templateData)
      return (candleCount, timeFrame, null);

    // Use the shared serializer (Newtonsoft) — the same one the flat pipeline uses to
    // enrich subscriptions. System.Text.Json would reject string enums (e.g. "CandlePart": "Close").
    var templateType = condition.Indicator.GetTemplate();
    var template = _serializer.DeserializeOrNull(templateData, templateType);

    if (template is ICandleCountDependentIndicatorTemplate candleDependent)
      candleCount = candleDependent.GetCandleCount();

    // The template's TimeFrame is authoritative: the same deserialized template goes to the
    // indicator handler, and TriggerChartService renders from it too. condition.TimeFrame is
    // only the fallback for templates without a timeframe (RoundPrice); on save,
    // RuleValidation.NormalizeConditionTimeFrames keeps the stored condition in sync.
    if (template is ITimeFrameDependentIndicatorTemplate timeFrameDependent)
      timeFrame = timeFrameDependent.TimeFrame;

    return (candleCount, timeFrame, template);
  }

  private async Task<IndicatorResult> EvaluateIndicatorAsync(
    Candle[] candles, RuleCondition condition, CancellationToken cancellationToken)
  {
    var template = ResolveTemplate(condition);
    // The indicator state cache is keyed by the condition id, so every condition keeps its
    // own stable slot (cross direction, Gap previous-candle date).
    var command = RouteByIndicator(condition.Indicator, condition.Id, condition.Ticker, template, candles);
    var result = await _mediator.Send(command, cancellationToken);

    if (result is not IndicatorResult indicatorResult)
      throw new InvalidOperationException("Invalid indicator result");

    return indicatorResult;
  }

  private IIndicatorTemplate ResolveTemplate(RuleCondition condition)
  {
    // Use the shared serializer (Newtonsoft) — System.Text.Json would reject string enums
    // (e.g. "CandlePart": "Close"); a missing/corrupt body falls back to the defaults.
    var templateType = condition.Indicator.GetTemplate();

    return condition.TemplateData is { Length: > 0 } templateData
      ? _serializer.DeserializeOrNull(templateData, templateType) as IIndicatorTemplate
        ?? (IIndicatorTemplate)Activator.CreateInstance(templateType)!
      : (IIndicatorTemplate)Activator.CreateInstance(templateType)!;
  }

  private static IndicatorCommandBase RouteByIndicator(
    Indicator indicator, Guid stateSlotId, string ticker, IIndicatorTemplate template, Candle[] candles)
  {
    return indicator switch
    {
      Indicator.EMA => IndicatorCommandBase.Create<EmaIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.SMA => IndicatorCommandBase.Create<SmaIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.StochRSI => IndicatorCommandBase.Create<StochRsiIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.RoundPrice => IndicatorCommandBase.Create<RoundPriceIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Gap => IndicatorCommandBase.Create<GapIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.RSI => IndicatorCommandBase.Create<RsiIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.MACD => IndicatorCommandBase.Create<MacdIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.BollingerBands => IndicatorCommandBase.Create<BollingerBandsIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.VWAP => IndicatorCommandBase.Create<VwapIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.ATR => IndicatorCommandBase.Create<AtrIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.OBV => IndicatorCommandBase.Create<ObvIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.ADX => IndicatorCommandBase.Create<AdxIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.SuperTrend => IndicatorCommandBase.Create<SuperTrendIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Stochastic => IndicatorCommandBase.Create<StochasticIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Ichimoku => IndicatorCommandBase.Create<IchimokuIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Volume => IndicatorCommandBase.Create<VolumeIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.PriceChange => IndicatorCommandBase.Create<PriceChangeIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.CCI => IndicatorCommandBase.Create<CciIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.MFI => IndicatorCommandBase.Create<MfiIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.WilliamsR => IndicatorCommandBase.Create<WilliamsRIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.CMO => IndicatorCommandBase.Create<CmoIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.TSI => IndicatorCommandBase.Create<TsiIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Keltner => IndicatorCommandBase.Create<KeltnerIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Donchian => IndicatorCommandBase.Create<DonchianIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.WMA => IndicatorCommandBase.Create<WmaIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.HMA => IndicatorCommandBase.Create<HmaIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.VWMA => IndicatorCommandBase.Create<VwmaIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.KAMA => IndicatorCommandBase.Create<KamaIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.PSAR => IndicatorCommandBase.Create<PsarIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Chandelier => IndicatorCommandBase.Create<ChandelierIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Aroon => IndicatorCommandBase.Create<AroonIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Vortex => IndicatorCommandBase.Create<VortexIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.OpenChange => IndicatorCommandBase.Create<OpenChangeIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.Week52HighLow => IndicatorCommandBase.Create<Week52HighLowIndicatorCommand>(stateSlotId, ticker, template, candles),
      Indicator.TrailingStop => IndicatorCommandBase.Create<TrailingStopIndicatorCommand>(stateSlotId, ticker, template, candles),
      _ => throw new ArgumentOutOfRangeException(nameof(indicator), indicator, null)
    };
  }
}
