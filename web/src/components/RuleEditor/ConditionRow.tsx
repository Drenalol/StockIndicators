import { useCallback, useMemo, useEffect, useRef } from 'react';
import type { Condition, InstrumentSelectItem } from './schema';
import { instrumentParts } from './schema';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import type { Translations } from '@/i18n';
import { getIndicatorFullNameWithAbbr, getIndicatorShortName, getParameterLabel, getTimeFrameLabel, getCandlePartLabel, getEnumValueLabel, TIMEFRAME_NAMES, CANDLE_PART_NAMES, isTimeFrameName } from '@/i18n/indicatorTranslations';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ParameterHint } from '@/components/ParameterHint';
import { IndicatorInfoDialog } from '@/components/IndicatorInfoDialog';
import { PercentField } from './PercentField';
import { InstrumentCombobox } from './InstrumentCombobox';

import type { MutableRefObject, ReactNode } from 'react';

interface Props {
  model: Condition;
  setModel: (m: Condition) => void;
  indicators: string[];
  template: Record<string, string> | null;
  /** Localized "what does this parameter mean" hints per parameter key (from the API) */
  descriptions?: Record<string, string> | null;
  onIndicatorChanged: (name: string) => void;
  t: Translations;
  locale: string;
  disableTicker?: boolean;
  searchInstruments: (query: string) => Promise<InstrumentSelectItem[]>;
  loadDefaultInstruments: () => Promise<InstrumentSelectItem[]>;
  resolveInstrument: (ticker: string, exchange: string | null) => InstrumentSelectItem | null;
  /** Instrument picked in the combobox */
  onInstrumentSelect: (item: InstrumentSelectItem) => void;
  originalRef?: MutableRefObject<{ condition: Condition }>;
  /** Move/delete buttons rendered inline in the instrument label row on sm+ (mobile keeps its own row) */
  headerActions?: ReactNode;
  /** Option names of custom template enums (Direction -> ["Both","Up","Down"]); rendered as selects */
  enumOptions?: Record<string, string[]> | null;
  /** Invariant template defaults used to prefill select/time parameters */
  defaults?: Record<string, string> | null;
}

function typeName(typeStr: string): 'int' | 'double' | 'string' | 'enum' {
  const t = typeStr.toLowerCase();
  if (t.includes('int') || t.includes('long') || t.includes('short')) return 'int';
  if (t.includes('double') || t.includes('float') || t.includes('decimal')) return 'double';
  if (t.includes('enum') || t.includes('candlepart') || t.includes('timeframe')) return 'enum';
  return 'string';
}

type FieldKind = 'timeframe' | 'candlePart' | 'select' | 'double' | 'int';

/**
 * Widget per template parameter. TimeFrame and CandlePart-like enum props keep their
 * built-in pickers; other template enums come through enumOptions (generic select).
 * Everything else is numeric.
 */
function fieldKind(key: string, typeStr: string, enumOptions?: Record<string, string[]> | null): FieldKind {
  if (key === 'TimeFrame') return 'timeframe';
  if (enumOptions?.[key]) return 'select';
  const tn = typeName(typeStr);
  if (tn === 'enum') return 'candlePart';
  if (tn === 'double') return 'double';
  return 'int';
}

const CANDLE_PARTS: string[] = [...CANDLE_PART_NAMES];

// Tolerances / channel bounds where 0 is a valid setting; every other numeric
// parameter must be positive (the API rejects zero and negative values)
const ZERO_ALLOWED_PARAMS = new Set(['Deviation', 'MinChannel', 'MaxChannel']);

// "14", "0.0005", "Day"; "0" is the backend's "no default" marker
function defaultFor(defaults: Record<string, string> | null | undefined, key: string): string | undefined {
  const raw = defaults?.[key];
  return raw && raw !== '0' ? raw : undefined;
}

// Prefill for parameters the template ships with a starting value: every parameter
// (enum or numeric) takes its API default when the model has no value yet.
function applyDefaults(
  m: Condition,
  template: Record<string, string> | null,
  enumOptions?: Record<string, string[]> | null,
  defaults?: Record<string, string> | null,
): Condition {
  if (!template) return m;
  const out = { ...m };
  for (const [key, typeStr] of Object.entries(template)) {
    const kind = fieldKind(key, typeStr, enumOptions);
    if (kind === 'double' && !(key in (out.doubleValues || {})) && !(key in (out.intValues || {}))) {
      const num = parseFloat(defaultFor(defaults, key) ?? '');
      if (Number.isFinite(num)) {
        out.doubleValues = { ...out.doubleValues, [key]: num };
      } else if (key === 'Deviation') {
        // No API default for Deviation: 0 is a valid (if strict) setting for it, and
        // 0.0005 is a sensible band out of the box
        out.doubleValues = { ...out.doubleValues, [key]: 0.0005 };
      }
    } else if (kind === 'int' && !(key in (out.intValues || {}))) {
      const num = parseFloat(defaultFor(defaults, key) ?? '');
      if (Number.isFinite(num)) {
        out.intValues = { ...out.intValues, [key]: num };
      }
    } else if (kind === 'timeframe') {
      if (!(key in (out.enumValues || {})) && !(key in (out.intValues || {}))) {
        const def = defaultFor(defaults, key);
        out.enumValues = { ...out.enumValues, [key]: def && isTimeFrameName(def) ? def : TIMEFRAME_NAMES[0] };
      }
    } else if (kind === 'candlePart') {
      if (!(key in (out.enumValues || {}))) {
        const def = defaultFor(defaults, key);
        const fallback = key === 'CandlePart' ? 'Close' : CANDLE_PARTS[0];
        out.enumValues = { ...out.enumValues, [key]: def && CANDLE_PARTS.includes(def) ? def : fallback };
      }
    } else if (kind === 'select') {
      // Custom template enum: the API default when it names a real option, the first
      // option otherwise. Kept in enumValues (serialized back as the option name).
      if (!(key in (out.enumValues || {}))) {
        const opts = enumOptions?.[key];
        const fromDefault = defaultFor(defaults, key);
        const defaultEnum = fromDefault && opts?.includes(fromDefault) ? fromDefault : opts?.[0];
        if (defaultEnum) out.enumValues = { ...out.enumValues, [key]: defaultEnum };
      }
    }
  }
  return out;
}

// Tailwind classes for the SelectTrigger (from select.tsx) minus "[&>span]:line-clamp-1":
// the variant's display:-webkit-box outspecifies .flex on the ticker label span, so the
// ExchangeLogo drops off the centered row (icon floats above the clipped ticker text).
// Truncation is already covered by min-w-0 + truncate on that span.
const SELECT_TRIGGER_CLASS =
  'flex h-10 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-50';

const READONLY_TRIGGER_CLASS = `${SELECT_TRIGGER_CLASS} cursor-not-allowed opacity-50`;

export function ConditionRow({ model, setModel, indicators, template, descriptions, onIndicatorChanged, t, locale, disableTicker, searchInstruments, loadDefaultInstruments, resolveInstrument, onInstrumentSelect, originalRef, headerActions, enumOptions, defaults }: Props) {
  const update = useCallback((patch: Partial<Condition>) => {
    setModel({ ...model, ...patch });
  }, [model, setModel]);

  const handleIndicatorChange = useCallback((name: string) => {
    if (model.indicator !== name) {
      // Single setModel call: two consecutive calls would both spread the same stale
      // `model` closure, and the second one would silently cancel the value reset,
      // leaking fields of the previous indicator into the saved template.
      setModel({
        ...model,
        indicator: name,
        intValues: {},
        doubleValues: {},
        decimalValues: {},
        enumValues: {},
        periods: undefined,
        candlePart: undefined,
      });
    } else {
      update({ indicator: name });
    }
    onIndicatorChanged(name);
  }, [update, setModel, onIndicatorChanged, model]);

  // Populate default values when a new template is loaded (the hoisted applyDefaults
  // keeps the effect deps honest).
  // Only non-zero API defaults are prefilled ("0" is the backend's "no default" marker):
  // a zero period or a zero gap percent is a broken indicator (always-true condition,
  // division by zero). Parameters without a default stay empty for the user and are
  // rejected by the API with a clear message if submitted.
  const prevTemplateRef = useRef<Record<string, string> | null>(null);

  useEffect(() => {
    if (!template || template === prevTemplateRef.current) return;
    prevTemplateRef.current = template;
    const withDefaults = applyDefaults(model, template, enumOptions, defaults);
    // Also update originalRef so changed comparison accounts for defaults
    if (originalRef) {
      originalRef.current.condition = withDefaults;
    }
    // Only propagate a real change: a preset applied while its template was still
    // loading already carries every value, and re-emitting an equal model on mount
    // would read as a manual edit (detaching the applied preset chip after a flash)
    if (JSON.stringify(withDefaults) !== JSON.stringify(model)) setModel(withDefaults);
  }, [template, enumOptions, defaults, originalRef, model, setModel]);

  const dynamicFields = useMemo(() => {
    if (!template) return [];
    return Object.entries(template).map(([key, typeStr]) => ({
      key,
      kind: fieldKind(key, typeStr, enumOptions),
    }));
  }, [template, enumOptions]);

  const lockedInstrument = instrumentParts(model.ticker, model.exchange ?? null, resolveInstrument);

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="space-y-2">
          {/* sm:min-h-7 matches the instrument label row (which carries the action buttons on sm+) so both selects stay top-aligned */}
          <div className="flex items-center gap-1 sm:min-h-7">
            <Label>{t.form.indicatorLabel}</Label>
            <IndicatorInfoDialog indicator={model.indicator} locale={locale} t={t} />
          </div>
          <Select value={model.indicator} onValueChange={handleIndicatorChange}>
              <SelectTrigger>
              {/* Explicit children disable Radix's portal of the selected item's content into the
                  trigger, so a chosen indicator shows only its short name (EMA); the dropdown
                  still lists the full localized names */}
              <SelectValue placeholder={t.form.selectIndicator}>
                {model.indicator ? getIndicatorShortName(model.indicator) : undefined}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {indicators.map((i) => (
                <SelectItem key={i} value={i}>{getIndicatorFullNameWithAbbr(i, t)}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <div className="flex items-center justify-between gap-1 sm:min-h-7">
            <Label>{t.form.instrumentLabel}</Label>
            {headerActions && <div className="hidden sm:flex sm:items-center sm:gap-0.5">{headerActions}</div>}
          </div>
          {disableTicker ? (
            <div className={READONLY_TRIGGER_CLASS} role="textbox" aria-readonly="true">
              <span className="flex min-w-0 items-center gap-2">
                <span className="truncate">{lockedInstrument.ticker || '-'}</span>
                {lockedInstrument.exchange && <ExchangeLogo name={lockedInstrument.exchange} size="lg" className="shrink-0" />}
              </span>
            </div>
          ) : (
            <InstrumentCombobox
              ticker={model.ticker}
              exchange={model.exchange ?? null}
              onSelect={onInstrumentSelect}
              search={searchInstruments}
              loadDefault={loadDefaultInstruments}
              resolve={resolveInstrument}
              t={t}
            />
          )}
          {disableTicker && (
            <p className="text-xs text-muted-foreground">{t.form.instrumentLocked}</p>
          )}
        </div>
      </div>

      {model.indicator && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-3">
          <Label className="text-xs font-medium text-muted-foreground uppercase tracking-wide sm:col-span-2">{t.form.parameters}</Label>
          {dynamicFields.map(({ key, kind }) => (
            <div key={key} className="space-y-1.5">
              <div className="flex items-center gap-1">
                <Label htmlFor={key} className="text-sm">{getParameterLabel(key, t)}</Label>
                <ParameterHint hint={descriptions?.[key]} t={t} />
              </div>
              {kind === 'timeframe' ? (
                <Select
                  value={(() => {
                    const intV = (model.intValues || {})[key];
                    if (typeof intV === 'number' && intV >= 0 && intV < TIMEFRAME_NAMES.length) return TIMEFRAME_NAMES[intV];
                    const enumV = (model.enumValues || {})[key];
                    if (typeof enumV === 'string' && isTimeFrameName(enumV)) return enumV;
                    return TIMEFRAME_NAMES[0];
                  })()}
                  onValueChange={(v) => {
                    // TimeFrame may have been loaded as an int (intValues); clear it so the
                    // freshly-picked name (enumValues) is what gets read back and saved.
                    const nextInt = { ...(model.intValues || {}) };
                    delete nextInt[key];
                    update({ intValues: nextInt, enumValues: { ...(model.enumValues || {}), [key]: v } });
                  }}
                >
                  <SelectTrigger id={key}><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {TIMEFRAME_NAMES.map((tf) => <SelectItem key={tf} value={tf}>{getTimeFrameLabel(tf, t)}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : kind === 'candlePart' ? (
                <Select
                  value={(model.enumValues || {})[key] ?? CANDLE_PARTS[0]}
                  onValueChange={(v) => update({ enumValues: { ...(model.enumValues || {}), [key]: v } })}
                >
                  <SelectTrigger id={key}><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {CANDLE_PARTS.map((cp) => <SelectItem key={cp} value={cp}>{getCandlePartLabel(cp, t)}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : kind === 'select' ? (
                <Select
                  value={(model.enumValues || {})[key] ?? enumOptions?.[key]?.[0]}
                  onValueChange={(v) => update({ enumValues: { ...(model.enumValues || {}), [key]: v } })}
                >
                  <SelectTrigger id={key}><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(enumOptions?.[key] ?? []).map((opt) => <SelectItem key={opt} value={opt}>{getEnumValueLabel(key, opt, t)}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : kind === 'double' ? (
                key === 'Deviation' ? (
                  <PercentField
                    id={key}
                    value={(model.doubleValues || {})[key] ?? (model.intValues || {})[key]}
                    onChange={(f) => {
                      const next = { ...(model.doubleValues || {}) };
                      // A Deviation of exactly 0 round-trips through intValues (parseTemplateData
                      // buckets integers there); clear it so the fraction bucket is authoritative
                      const nextInt = { ...(model.intValues || {}) };
                      delete nextInt[key];
                      if (f === undefined) delete next[key]; else next[key] = f;
                      update({ intValues: nextInt, doubleValues: next });
                    }}
                  />
                ) : (
                  <Input
                    id={key} type="number" step="any"
                    min={ZERO_ALLOWED_PARAMS.has(key) ? 0 : 0.0001}
                    // Doubles saved as whole numbers (RSI 70/30, ADX 25, ...) round-trip
                    // through intValues (parseTemplateData buckets by Number.isInteger);
                    // same fallback as Deviation above
                    value={(model.doubleValues || {})[key] ?? (model.intValues || {})[key] ?? ''}
                    onChange={(e) => { const v = e.target.value; const next = { ...(model.doubleValues || {}) }; const nextInt = { ...(model.intValues || {}) }; delete nextInt[key]; if (v === '') delete next[key]; else next[key] = parseFloat(v); update({ intValues: nextInt, doubleValues: next }); }}
                  />
                )
              ) : (
                <Input
                  id={key} type="number" step="1" min={1}
                  value={(model.intValues || {})[key] ?? ''}
                  onChange={(e) => { const v = e.target.value; const next = { ...(model.intValues || {}) }; if (v === '') delete next[key]; else next[key] = parseInt(v, 10); update({ intValues: next }); }}
                />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
