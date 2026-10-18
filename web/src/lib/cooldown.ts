// Slider math for the check cooldown, the same approach as lib/deviation.ts:
// a log scale where every step multiplies the value by a constant factor, so the
// slider moves slowly at the left edge (left of ~1 hour several positions map to
// the same whole minute) while steps towards the 24 h edge are 5+ minutes.
// The manual input next to the slider is the precise path for exact values.

import type { Translations } from '@/i18n';

export const MIN_COOLDOWN_MINUTES = 1;
export const MAX_COOLDOWN_MINUTES = 24 * 60;
export const COOLDOWN_SLIDER_MAX_POS = 1000;

/** Slider position → whole minutes; clamped to the [1 min, 24 h] range */
export function sliderPosToMinutes(pos: number): number {
  if (pos <= 0) return MIN_COOLDOWN_MINUTES;
  if (pos >= COOLDOWN_SLIDER_MAX_POS) return MAX_COOLDOWN_MINUTES;
  return Math.round(Math.pow(MAX_COOLDOWN_MINUTES, pos / COOLDOWN_SLIDER_MAX_POS));
}

/** Minutes → slider position, inverse of sliderPosToMinutes; out-of-range values clamp */
export function minutesToSliderPos(minutes: number): number {
  if (!Number.isFinite(minutes)) return 0;
  const m = Math.min(MAX_COOLDOWN_MINUTES, Math.max(MIN_COOLDOWN_MINUTES, minutes));
  return Math.round(COOLDOWN_SLIDER_MAX_POS * (Math.log(m) / Math.log(MAX_COOLDOWN_MINUTES)));
}

/**
 * Typed cooldown text → minutes, the inverse of formatCooldown. Accepts plain
 * minutes ("22") and the display forms with units ("22 мин", "22 min", "2 ч",
 * "2h", "1 ч 30 мин"), localized and English tokens alike. Null for partial or
 * junk text, so typing "abc" never propagates and blur can revert the field.
 */
export function parseCooldownText(raw: string): number | null {
  const s = raw.trim().toLowerCase().replace(/\s+/g, ' ');
  if (!s) return null;
  const MIN_TOK = 'мин|min|m';
  const HOUR_TOK = 'ч|h';
  let m = /^(\d+)$/.exec(s);
  if (m) return Number(m[1]);
  m = new RegExp(`^(\\d+)\\s*(?:${MIN_TOK})$`).exec(s);
  if (m) return Number(m[1]);
  m = new RegExp(`^(\\d+)\\s*(?:${HOUR_TOK})$`).exec(s);
  if (m) return Number(m[1]) * 60;
  m = new RegExp(`^(\\d+)\\s*(?:${HOUR_TOK})\\s*(\\d+)\\s*(?:${MIN_TOK})?$`).exec(s);
  if (m) return Number(m[1]) * 60 + Number(m[2]);
  return null;
}

/**
 * Whole minutes → compact localized text: "25 мин", "1 ч 30 мин", "6 ч".
 * Compact unit tokens ("ч"/"мин", "h"/"min" elsewhere) keep any value from
 * needing grammatical plural agreement.
 */
export function formatCooldown(minutes: number, t: Translations): string {
  const minSuffix = t.cooldown.minSuffix;
  if (minutes < 60) return `${minutes} ${minSuffix}`;
  const hours = Math.floor(minutes / 60);
  const rem = minutes % 60;
  return rem === 0
    ? `${hours} ${t.cooldown.hourSuffix}`
    : `${hours} ${t.cooldown.hourSuffix} ${rem} ${minSuffix}`;
}

/**
 * Subscription cooldownSeconds → localized display text (whole minutes via
 * formatCooldown); missing or non-finite seconds render as a plain dash.
 */
export function formatCooldownSeconds(seconds: number | null | undefined, t: Translations): string {
  if (seconds == null || !Number.isFinite(seconds)) return '-';
  return formatCooldown(Math.floor(seconds / 60), t);
}
