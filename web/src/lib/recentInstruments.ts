import type { InstrumentSelectItem } from '@/components/RuleEditor/schema';

const STORAGE_KEY = 'si-recent-instruments';
const MAX_RECENT = 8;

const keyOf = (i: InstrumentSelectItem) => `${i.ticker}|${i.exchangeName ?? ''}`.toLowerCase();

function isItem(value: unknown): value is InstrumentSelectItem {
  if (!value || typeof value !== 'object') return false;
  const i = value as Record<string, unknown>;
  return typeof i.ticker === 'string' && i.ticker !== '';
}

/** Last instruments picked in the rule editor, most recent first (localStorage, best-effort). */
export function loadRecentInstruments(): InstrumentSelectItem[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    // Rebuild from known fields only: entries stored by older app versions may still
    // carry a "source" property, which must not resurface in the UI.
    return Array.isArray(parsed)
      ? parsed.filter(isItem).map(i => ({ ticker: i.ticker, exchangeName: i.exchangeName ?? null, name: i.name ?? null }))
      : [];
  } catch {
    return [];
  }
}

/** Remember an instrument for the next picker session; deduped, capped, never throws. */
export function pushRecentInstrument(item: InstrumentSelectItem): void {
  try {
    const rest = loadRecentInstruments().filter((i) => keyOf(i) !== keyOf(item));
    localStorage.setItem(STORAGE_KEY, JSON.stringify([item, ...rest].slice(0, MAX_RECENT)));
  } catch {
    // private mode / quota exceeded - recents are best-effort
  }
}
