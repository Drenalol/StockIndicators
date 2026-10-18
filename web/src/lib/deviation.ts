// Deviation is stored as a fraction of the indicator value (0.0005 = 0.05%).
// The UI shows and edits it in percent; these helpers are the single
// conversion point between the stored fraction and the displayed percent.

export const DEVIATION_SLIDER_MAX_POS = 1000;

const MIN_PERCENT = 0.01;
const MAX_PERCENT = 100;

/**
 * Percent number to display text, with float noise trimmed (0.0500000023842 → "0.05").
 * Seven significant digits: backend defaults like `0.0005F` are float32 literals
 * widened to double, so garbage starts at the 8th digit (0.05000000237...).
 */
export function formatPercent(pct: number): string {
  return String(parseFloat(pct.toPrecision(7)));
}

/** Stored fraction to read-only percent text (0.0005 → "0.05%") */
export function formatDeviationPercent(fraction: number): string {
  return `${formatPercent(fraction * 100)}%`;
}

/**
 * Slider position → percent on a piecewise scale: 0 stays 0 (left edge),
 * then a log scale from 0.01% to 100% so both 0.05% (MA bands) and
 * 10% (StochRSI crossover tolerance) are comfortably draggable.
 */
export function sliderPosToPercent(pos: number): number {
  if (pos <= 0) return 0;
  return MIN_PERCENT * Math.pow(MAX_PERCENT / MIN_PERCENT, pos / DEVIATION_SLIDER_MAX_POS);
}

/** Percent → slider position, inverse of sliderPosToPercent; out-of-range values clamp */
export function percentToSliderPos(pct: number): number {
  if (pct <= 0) return 0;
  const pos = DEVIATION_SLIDER_MAX_POS * (Math.log(pct / MIN_PERCENT) / Math.log(MAX_PERCENT / MIN_PERCENT));
  return Math.min(DEVIATION_SLIDER_MAX_POS, Math.max(1, Math.round(pos)));
}
