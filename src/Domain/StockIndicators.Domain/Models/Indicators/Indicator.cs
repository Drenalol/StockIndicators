// ReSharper disable InconsistentNaming

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using StockIndicators.Domain.Models.Indicators.Attributes;
using StockIndicators.Domain.Models.Indicators.Templates;

namespace StockIndicators.Domain.Models.Indicators;

[JsonConverter(typeof(StringEnumConverter))]
public enum Indicator
{
  [IndicatorTemplate(typeof(MaIndicatorTemplate))]
  SMA,

  [IndicatorTemplate(typeof(EmaIndicatorTemplate))]
  EMA,

  [IndicatorTemplate(typeof(StochRsiIndicatorTemplate))]
  StochRSI,

  [IndicatorTemplate(typeof(RoundPriceIndicatorTemplate))]
  RoundPrice,

  [IndicatorTemplate(typeof(GapIndicatorTemplate))]
  Gap,

  [IndicatorTemplate(typeof(MacdIndicatorTemplate))]
  MACD,

  [IndicatorTemplate(typeof(RsiIndicatorTemplate))]
  RSI,

  [IndicatorTemplate(typeof(BollingerBandsIndicatorTemplate))]
  BollingerBands,

  [IndicatorTemplate(typeof(SuperTrendIndicatorTemplate))]
  SuperTrend,

  [IndicatorTemplate(typeof(VwapIndicatorTemplate))]
  VWAP,

  [IndicatorTemplate(typeof(AdxIndicatorTemplate))]
  ADX,

  [IndicatorTemplate(typeof(StochasticIndicatorTemplate))]
  Stochastic,

  [IndicatorTemplate(typeof(AtrIndicatorTemplate))]
  ATR,

  [IndicatorTemplate(typeof(IchimokuIndicatorTemplate))]
  Ichimoku,

  [IndicatorTemplate(typeof(ObvIndicatorTemplate))]
  OBV,

  [IndicatorTemplate(typeof(VolumeIndicatorTemplate))]
  Volume,

  [IndicatorTemplate(typeof(PriceChangeIndicatorTemplate))]
  PriceChange,

  [IndicatorTemplate(typeof(CciIndicatorTemplate))]
  CCI,

  [IndicatorTemplate(typeof(MfiIndicatorTemplate))]
  MFI,

  [IndicatorTemplate(typeof(WilliamsRIndicatorTemplate))]
  WilliamsR,

  [IndicatorTemplate(typeof(CmoIndicatorTemplate))]
  CMO,

  [IndicatorTemplate(typeof(TsiIndicatorTemplate))]
  TSI,

  [IndicatorTemplate(typeof(KeltnerIndicatorTemplate))]
  Keltner,

  [IndicatorTemplate(typeof(DonchianIndicatorTemplate))]
  Donchian,

  [IndicatorTemplate(typeof(WmaIndicatorTemplate))]
  WMA,

  [IndicatorTemplate(typeof(HmaIndicatorTemplate))]
  HMA,

  [IndicatorTemplate(typeof(VwmaIndicatorTemplate))]
  VWMA,

  [IndicatorTemplate(typeof(KamaIndicatorTemplate))]
  KAMA,

  [IndicatorTemplate(typeof(PsarIndicatorTemplate))]
  PSAR,

  [IndicatorTemplate(typeof(ChandelierIndicatorTemplate))]
  Chandelier,

  [IndicatorTemplate(typeof(AroonIndicatorTemplate))]
  Aroon,

  [IndicatorTemplate(typeof(VortexIndicatorTemplate))]
  Vortex,

  [IndicatorTemplate(typeof(OpenChangeIndicatorTemplate))]
  OpenChange,

  [IndicatorTemplate(typeof(Week52HighLowIndicatorTemplate))]
  Week52HighLow,

  [IndicatorTemplate(typeof(TrailingStopIndicatorTemplate))]
  TrailingStop,
}
