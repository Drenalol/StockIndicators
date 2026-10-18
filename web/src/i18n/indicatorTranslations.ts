import type { Translations } from './types';

/** Every indicator id the app knows (the backend enum's wire names); the subscription
 *  list's indicator filter offers them by i18n name. */
export const KNOWN_INDICATORS = [
  'SMA', 'EMA', 'StochRSI', 'RoundPrice', 'Gap',
  'MACD', 'RSI', 'BollingerBands', 'SuperTrend', 'VWAP',
  'ADX', 'Stochastic', 'ATR', 'Ichimoku', 'OBV',
  'Volume', 'PriceChange',
  'CCI', 'MFI', 'WilliamsR', 'CMO', 'TSI',
  'Keltner', 'Donchian', 'WMA', 'HMA', 'VWMA', 'KAMA',
  'PSAR', 'Chandelier', 'Aroon', 'Vortex',
  'OpenChange', 'Week52HighLow', 'TrailingStop'
] as const;

const KNOWN_PARAMS = [
  'Periods', 'Period', 'SignalPeriod', 'MinChannel', 'MaxChannel',
  'Deviation', 'TimeFrame', 'CandlePart', 'Percent', 'Accuracy',
  'FastPeriods', 'SlowPeriods', 'SignalPeriods', 'SmoothPeriod',
  'StandardDeviation', 'Overbought', 'Oversold', 'Multiplier',
  'AdxThreshold', 'MinPercent', 'TenkanPeriod', 'KijunPeriod', 'SenkouBPeriod',
  'EmaPeriod', 'RsiPeriod', 'StochPeriod', 'Direction',
  'AtrPeriod', 'LongPeriods', 'ErPeriods', 'MaxAccelerationFactor', 'Type',
  'Weeks', 'Extreme', 'Mode', 'PullbackPercent', 'ThresholdType', 'Value'
] as const;

/**
 * UI metadata only: which category badge an indicator card shows. The full indicator
 * description lives in the backend (IndicatorHint attribute); categories are a
 * presentation concern and need no API round-trip.
 */
const INDICATOR_CATEGORIES = {
  SMA: 'trend',
  EMA: 'trend',
  SuperTrend: 'trend',
  ADX: 'trend',
  Ichimoku: 'trend',
  RSI: 'momentum',
  StochRSI: 'momentum',
  Stochastic: 'momentum',
  MACD: 'momentum',
  BollingerBands: 'volatility',
  ATR: 'volatility',
  VWAP: 'volatility',
  OBV: 'volume',
  Volume: 'volume',
  Gap: 'priceLevels',
  RoundPrice: 'priceLevels',
  PriceChange: 'momentum',
  CCI: 'momentum',
  MFI: 'momentum',
  WilliamsR: 'momentum',
  CMO: 'momentum',
  TSI: 'momentum',
  Keltner: 'volatility',
  Donchian: 'volatility',
  WMA: 'trend',
  HMA: 'trend',
  VWMA: 'trend',
  KAMA: 'trend',
  PSAR: 'trend',
  Chandelier: 'trend',
  Aroon: 'trend',
  Vortex: 'trend',
  OpenChange: 'momentum',
  Week52HighLow: 'priceLevels',
  TrailingStop: 'trend',
} as const;

export type IndicatorCategory = (typeof INDICATOR_CATEGORIES)[keyof typeof INDICATOR_CATEGORIES];

export function getIndicatorCategory(name: string): IndicatorCategory {
  return (INDICATOR_CATEGORIES as Record<string, IndicatorCategory>)[name] ?? 'trend';
}

export function getIndicatorCategoryLabel(name: string, t: Translations): string {
  return t.indicators.categories[getIndicatorCategory(name)];
}

export function getIndicatorName(name: string, t: Translations): string {
  return t.indicators.names?.[name as (typeof KNOWN_INDICATORS)[number]] ?? name;
}

/** Return the short English key (SMA, EMA, StochRSI, etc.) */
export function getIndicatorShortName(name: string): string {
  return name;
}

/** Return "Localized name | SHORT" for edit/create/detail views */
export function getIndicatorFullNameWithAbbr(name: string, t: Translations): string {
  const fullName = getIndicatorName(name, t);
  const short = getIndicatorShortName(name);
  // If the localized name already equals the short name (English locale), just return it
  if (fullName === short) return short;
  return `${fullName} | ${short}`;
}

export function getParameterLabel(key: string, t: Translations): string {
  return t.indicators.parameters?.[key as (typeof KNOWN_PARAMS)[number]] ?? key;
}

// Single source for the backend enum StockIndicators.Contracts.Candles.TimeFrame
// (Minute=0, Hour=1, Day=2, Week=3, Month=4)
export const TIMEFRAME_NAMES = ['Minute', 'Hour', 'Day', 'Week', 'Month'] as const;
export type TimeFrameName = (typeof TIMEFRAME_NAMES)[number];

export function isTimeFrameName(value: string): value is TimeFrameName {
  return (TIMEFRAME_NAMES as readonly string[]).includes(value);
}

export function getTimeFrameLabel(value: string | number, t: Translations): string {
  const key = typeof value === 'number' ? (TIMEFRAME_NAMES[value] ?? String(value)) : value;
  return t.indicators?.enums?.timeFrame?.[key as TimeFrameName] ?? key;
}

// Single source for the backend enum StockIndicators.Domain.Models.Indicators.CandlePart
export const CANDLE_PART_NAMES = ['Open', 'High', 'Low', 'Close'] as const;

export function getCandlePartLabel(value: string, t: Translations): string {
  return t.indicators?.enums?.candlePart?.[value as 'Open' | 'High' | 'Low' | 'Close'] ?? value;
}

/**
 * Possible enum values for a template parameter: TimeFrame and CandlePart use the
 * built-in client lists, the other template enums come from the schema's
 * enumOptions. Null for non-enum parameters.
 */
export function getParameterEnumOptions(
  key: string,
  enumOptions?: Record<string, string[]> | null,
): string[] | null {
  if (key === 'TimeFrame') return [...TIMEFRAME_NAMES];
  if (key === 'CandlePart') return [...CANDLE_PART_NAMES];
  return enumOptions?.[key] ?? null;
}

/** Localized enum values comma-joined for tooltips, null for non-enum parameters */
export function getParameterEnumOptionsLabel(
  key: string,
  enumOptions: Record<string, string[]> | null | undefined,
  t: Translations,
): string | null {
  const options = getParameterEnumOptions(key, enumOptions);
  if (!options) return null;

  return options
    .map((value) =>
      key === 'TimeFrame' ? getTimeFrameLabel(value, t)
      : key === 'CandlePart' ? getCandlePartLabel(value, t)
      : getEnumValueLabel(key, value, t))
    .join(', ');
}

/**
 * Human-readable name of a template parameter's .NET type (users do not read
 * "Int32"/"Double"); anything unrecognized keeps its raw name.
 */
export function getTypeLabel(type: string, t: Translations): string {
  switch (type) {
    case 'Int32':
    case 'Int64':
      return t.indicators?.types?.integer ?? type;
    case 'Double':
    case 'Decimal':
    case 'Single':
      return t.indicators?.types?.decimal ?? type;
    default:
      return type;
  }
}

/**
 * Localized label for a custom template enum value (the parameter key lowercased
 * selects the table, e.g. Direction -> t.indicators.enums.direction). Unknown
 * keys/values fall back to the raw invariant name.
 */
export function getEnumValueLabel(paramKey: string, value: string, t: Translations): string {
  const table = (t.indicators?.enums as Record<string, Record<string, string>> | undefined)?.[paramKey.toLowerCase()];
  return table?.[value] ?? value;
}
