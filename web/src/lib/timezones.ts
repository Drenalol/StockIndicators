/** IANA time zone of the current browser (e.g. "Europe/Moscow"), null when unavailable. */
export function getBrowserTimezone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone ?? null;
  } catch {
    return null;
  }
}

/** All IANA time zone ids known to the browser, sorted. */
export function getSupportedTimezones(): string[] {
  try {
    return typeof Intl.supportedValuesOf === 'function'
      ? [...Intl.supportedValuesOf('timeZone')].sort()
      : [];
  } catch {
    return [];
  }
}

/** Short UTC offset of a time zone at the current moment, e.g. "GMT+3". */
export function getTzOffset(tz: string): string {
  const cached = tzOffsetCache.get(tz);
  if (cached !== undefined) return cached;
  let offset = '';
  try {
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: tz, timeZoneName: 'shortOffset' })
      .formatToParts(new Date());
    offset = parts.find((p) => p.type === 'timeZoneName')?.value ?? '';
  } catch {
    offset = '';
  }
  tzOffsetCache.set(tz, offset);
  return offset;
}

/** "Europe/Moscow (GMT+3)" — display label for a time zone id. */
export function formatTzLabel(tz: string): string {
  const offset = getTzOffset(tz);
  return offset ? `${tz} (${offset})` : tz;
}

export interface TimezoneGroupItem {
  zone: string;
  offset: string;
}

export interface TimezoneGroup {
  region: string;
  items: TimezoneGroupItem[];
}

// Session-level cache: a zone's offset only changes on DST transitions, so one
// Intl.DateTimeFormat per zone per session is enough (labels refresh on reload).
const tzOffsetCache = new Map<string, string>();

let timezoneGroupsSingleton: TimezoneGroup[] | null = null;

/**
 * All IANA time zones grouped by their region prefix, each with a precomputed
 * offset label. Built lazily once per session — Intl.DateTimeFormat is
 * expensive, and the list is static for the lifetime of the page.
 */
export function getTimezoneGroups(): TimezoneGroup[] {
  if (timezoneGroupsSingleton) return timezoneGroupsSingleton;
  const byRegion = new Map<string, TimezoneGroupItem[]>();
  for (const zone of getSupportedTimezones()) {
    const region = zone.split('/')[0];
    const list = byRegion.get(region) ?? [];
    list.push({ zone, offset: getTzOffset(zone) });
    byRegion.set(region, list);
  }
  timezoneGroupsSingleton = [...byRegion.entries()]
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([region, items]) => ({ region, items }));
  return timezoneGroupsSingleton;
}
