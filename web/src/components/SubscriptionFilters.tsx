import { useState } from 'react';
import { Check, ChevronDown, X } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { getIndicatorName, getIndicatorShortName, KNOWN_INDICATORS } from '@/i18n/indicatorTranslations';
import type { useTranslation } from '@/i18n';
import { cn } from '@/lib/utils';

/** The chip state of the subscriptions list; every value lives in a URL param. */
export interface SubscriptionFilterValue {
  statuses: string[];
  type: string;
  indicator: string;
  exchange: string;
  triggered: boolean;
}

export function hasSubscriptionFilters(v: SubscriptionFilterValue): boolean {
  return v.statuses.length > 0 || v.type !== '' || v.indicator !== '' || v.exchange !== '' || v.triggered;
}

type T = ReturnType<typeof useTranslation>['t'];

const TYPE_OPTIONS: { value: string; labelKey: keyof T['instruments'] }[] = [
  { value: 'Stock', labelKey: 'type_stock' },
  { value: 'Bond', labelKey: 'type_bond' },
  { value: 'Etf', labelKey: 'type_etf' },
  { value: 'Option', labelKey: 'type_option' },
  { value: 'Future', labelKey: 'type_future' },
  { value: 'Crypto', labelKey: 'type_crypto' },
];

const chipBase =
  'inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-full border px-4 text-sm transition-colors';
const chipOn = 'border-primary bg-primary/10 font-medium text-primary';
const chipOff = 'bg-background hover:bg-accent hover:text-accent-foreground';

function ToggleChip({ active, onClick, children, ariaLabel }: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
  ariaLabel?: string;
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      aria-label={ariaLabel}
      onClick={onClick}
      className={cn(chipBase, active ? chipOn : chipOff)}
    >
      {children}
    </button>
  );
}

// Popover chip with a short static option list (types, exchanges, rule shape)
function SelectChip({ label, value, options, allLabel, onPick, disabled }: {
  label: string;
  value: string;
  options: { value: string; label: string }[];
  allLabel: string;
  onPick: (value: string) => void;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const selected = options.find(o => o.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          disabled={disabled}
          aria-expanded={open}
          className={cn(chipBase, selected ? chipOn : chipOff)}
        >
          {selected ? selected.label : label}
          <ChevronDown className="h-3.5 w-3.5 opacity-70" />
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-auto p-1">
        <div className="flex flex-col">
          <button
            type="button"
            onClick={() => { onPick(''); setOpen(false); }}
            className={cn('flex min-h-10 items-center rounded-md px-2 text-sm hover:bg-accent', !value && 'font-medium')}
          >
            {allLabel}
          </button>
          {options.map(o => (
            <button
              key={o.value}
              type="button"
              onClick={() => { onPick(o.value); setOpen(false); }}
              className={cn('flex min-h-10 items-center justify-between gap-2 rounded-md px-2 text-sm hover:bg-accent', value === o.value && 'font-medium')}
            >
              <span className="min-w-0 truncate">{o.label}</span>
              {value === o.value && <Check className="h-4 w-4 shrink-0" />}
            </button>
          ))}
        </div>
      </PopoverContent>
    </Popover>
  );
}

// One chip for the three subscription statuses: the popover multi-selects them
// (a checkbox list), the chip is active while at least one status is picked
function StatusChip({ values, options, label, onToggle }: {
  values: string[];
  options: { value: string; label: string }[];
  label: string;
  onToggle: (value: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const single = values.length === 1 ? options.find(o => o.value === values[0]) : null;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-expanded={open}
          className={cn(chipBase, values.length > 0 ? chipOn : chipOff)}
        >
          {single ? single.label : label}
          <ChevronDown className="h-3.5 w-3.5 opacity-70" />
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-auto p-1">
        <div className="flex flex-col">
          {options.map(o => (
            <button
              key={o.value}
              type="button"
              role="checkbox"
              aria-checked={values.includes(o.value)}
              onClick={() => onToggle(o.value)}
              className={cn('flex min-h-10 items-center justify-between gap-2 rounded-md px-2 text-sm hover:bg-accent', values.includes(o.value) && 'font-medium')}
            >
              <span className="truncate">{o.label}</span>
              {values.includes(o.value) && <Check className="h-4 w-4 shrink-0" />}
            </button>
          ))}
        </div>
      </PopoverContent>
    </Popover>
  );
}

// Indicator picker: the list is long, so it gets the command search box
function IndicatorChip({ value, allLabel, searchPlaceholder, label, onPick, t }: {
  value: string;
  allLabel: string;
  searchPlaceholder: string;
  label: string;
  onPick: (value: string) => void;
  t: T;
}) {
  const [open, setOpen] = useState(false);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-expanded={open}
          className={cn(chipBase, value ? chipOn : chipOff)}
        >
          {value ? getIndicatorShortName(value) : label}
          <ChevronDown className="h-3.5 w-3.5 opacity-70" />
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-72 p-0">
        <Command shouldFilter>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            <CommandItem value="__all__" onSelect={() => { onPick(''); setOpen(false); }}>
              {allLabel}
            </CommandItem>
            {KNOWN_INDICATORS.map(indicator => (
              <CommandItem
                key={indicator}
                value={`${indicator} ${getIndicatorName(indicator, t)}`.toLowerCase()}
                onSelect={() => { onPick(indicator); setOpen(false); }}
              >
                <span className="min-w-0 truncate">{getIndicatorName(indicator, t)}</span>
                {value === indicator && <Check className="ml-auto h-4 w-4 shrink-0" />}
              </CommandItem>
            ))}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

/**
 * Chip filters above the subscriptions list: one status chip multi-selecting the three
 * subscription statuses, instrument type / indicator / exchange popovers, the "triggered
 * in the last week" toggle and a clear chip. The component is controlled: the page keeps
 * every value in a URL param, so the filtered list survives reloads and Back/Forward.
 */
export function SubscriptionFilters({ value, exchanges, onChange, onClear, t }: {
  value: SubscriptionFilterValue;
  exchanges: string[];
  onChange: (mutate: (v: SubscriptionFilterValue) => SubscriptionFilterValue) => void;
  onClear: () => void;
  t: T;
}) {
  const f = t.subscriptions.filters;

  const toggleStatus = (status: string) =>
    onChange(v => ({ ...v, statuses: v.statuses.includes(status) ? v.statuses.filter(s => s !== status) : [...v.statuses, status] }));

  const statusOptions = [
    { value: 'Active', label: t.subscriptions.status.active },
    { value: 'Inactive', label: t.subscriptions.status.inactive },
  ];

  return (
    <div className="flex flex-wrap items-center gap-2 pt-3" role="group" aria-label={f.aria}>
      <StatusChip
        values={value.statuses}
        options={statusOptions}
        label={f.status}
        onToggle={toggleStatus}
      />

      <SelectChip
        label={f.type}
        allLabel={t.instruments.type_all}
        value={value.type}
        options={TYPE_OPTIONS.map(o => ({ value: o.value, label: t.instruments[o.labelKey] as string }))}
        onPick={type => onChange(v => ({ ...v, type }))}
      />

      <IndicatorChip
        label={f.indicator}
        allLabel={f.indicatorAll}
        searchPlaceholder={t.indicators.searchPlaceholder}
        value={value.indicator}
        onPick={indicator => onChange(v => ({ ...v, indicator }))}
        t={t}
      />

      <SelectChip
        label={f.exchange}
        allLabel={f.exchangeAll}
        value={value.exchange}
        options={exchanges.map(ex => ({ value: ex, label: ex }))}
        onPick={exchange => onChange(v => ({ ...v, exchange }))}
      />

      <ToggleChip active={value.triggered} onClick={() => onChange(v => ({ ...v, triggered: !v.triggered }))}>
        {f.triggered}
      </ToggleChip>

      {hasSubscriptionFilters(value) && (
        <ToggleChip active onClick={onClear} ariaLabel={f.clear}>
          <X className="h-3.5 w-3.5" />
          {f.clear}
        </ToggleChip>
      )}
    </div>
  );
}
