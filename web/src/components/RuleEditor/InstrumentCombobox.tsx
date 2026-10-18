import { useCallback, useRef, useState } from 'react';
import { Check, ChevronsUpDown, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { cn } from '@/lib/utils';
import { loadRecentInstruments, pushRecentInstrument } from '@/lib/recentInstruments';
import type { Translations } from '@/i18n';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InstrumentLogo } from '@/components/InstrumentLogo';
import { instrumentKey, instrumentParts, type InstrumentSelectItem } from './schema';

interface Props {
  ticker: string;
  exchange: string | null;
  onSelect: (item: InstrumentSelectItem) => void;
  /** Server-side search (ticker/name/exchange), no debouncing here */
  search: (query: string) => Promise<InstrumentSelectItem[]>;
  /** Default list shown when the query is empty */
  loadDefault: () => Promise<InstrumentSelectItem[]>;
  /** Instrument cache lookup, used for the trigger label */
  resolve: (ticker: string, exchange: string | null) => InstrumentSelectItem | null;
  disabled?: boolean;
  t: Translations;
}

// Keystrokes inside the open dropdown are cheap local state updates; the API call is debounced
const SEARCH_DEBOUNCE_MS = 250;

export function InstrumentCombobox({ ticker, exchange, onSelect, search, loadDefault, resolve, disabled, t }: Props) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [items, setItems] = useState<InstrumentSelectItem[]>([]);
  const [recents, setRecents] = useState<InstrumentSelectItem[]>([]);
  const [loading, setLoading] = useState(false);
  const seqRef = useRef(0);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const selectedKey = ticker ? instrumentKey(ticker, exchange) : '';
  const triggerParts = instrumentParts(ticker, exchange, resolve);

  const runSearch = useCallback((q: string) => {
    const seq = ++seqRef.current;
    if (timerRef.current) clearTimeout(timerRef.current);
    const trimmed = q.trim();
    const fetcher = trimmed ? () => search(trimmed) : loadDefault;
    setLoading(true);
    fetcher()
      .then((r) => { if (seq === seqRef.current) setItems(r); })
      .catch(() => { if (seq === seqRef.current) setItems([]); })
      .finally(() => { if (seq === seqRef.current) setLoading(false); });
  }, [search, loadDefault]);

  const handleOpenChange = useCallback((next: boolean) => {
    setOpen(next);
    if (next) {
      setQuery('');
      setRecents(loadRecentInstruments());
      runSearch('');
    } else {
      // Invalidate in-flight responses so a late arrival can never flash into a closed dropdown
      if (timerRef.current) clearTimeout(timerRef.current);
      seqRef.current++;
      setLoading(false);
    }
  }, [runSearch]);

  const handleInput = useCallback((q: string) => {
    setQuery(q);
    if (timerRef.current) clearTimeout(timerRef.current);
    if (!q.trim()) {
      runSearch('');
      return;
    }
    timerRef.current = setTimeout(() => runSearch(q), SEARCH_DEBOUNCE_MS);
  }, [runSearch]);

  const handleSelect = useCallback((item: InstrumentSelectItem) => {
    pushRecentInstrument(item);
    onSelect(item);
    setOpen(false);
  }, [onSelect]);

  // Recents render first on an empty query; the selected instrument is always present in the list
  const seen = new Set<string>();
  const dedup = (list: InstrumentSelectItem[]) =>
    list.filter((i) => {
      const k = instrumentKey(i.ticker, i.exchangeName);
      if (seen.has(k)) return false;
      seen.add(k);
      return true;
    });
  const recentItems = query.trim() ? [] : dedup(recents);
  const mainItems = dedup(items);
  if (ticker) {
    const selected = resolve(ticker, exchange);
    if (selected && !seen.has(selectedKey)) mainItems.unshift(selected);
  }

  const renderItem = (item: InstrumentSelectItem) => {
    const k = instrumentKey(item.ticker, item.exchangeName);
    return (
      <CommandItem key={k} value={k} onSelect={() => handleSelect(item)} className="min-h-11 sm:min-h-0">
        <InstrumentLogo ticker={item.ticker} exchangeName={item.exchangeName} size="xs" className="mr-2" />
        <span className="truncate">{item.name ? `${item.name} (${item.ticker})` : item.ticker}</span>
        {item.exchangeName && (
          <ExchangeLogo name={item.exchangeName} size="lg" className="ml-auto shrink-0 pl-2" />
        )}
        {k === selectedKey && <Check className={cn('shrink-0', !item.exchangeName && 'ml-auto')} />}
      </CommandItem>
    );
  };

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className={cn('w-full justify-between font-normal', !ticker && 'text-muted-foreground')}
        >
          {triggerParts.ticker ? (
            <span className="flex min-w-0 items-center gap-2">
              <span className="truncate">{triggerParts.ticker}</span>
              {triggerParts.exchange && <ExchangeLogo name={triggerParts.exchange} size="lg" className="shrink-0" />}
            </span>
          ) : (
            <span className="truncate">{t.form.selectInstrument}</span>
          )}
          {loading && open
            ? <Loader2 className="h-4 w-4 shrink-0 animate-spin opacity-50" />
            : <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-(--radix-popover-trigger-width) p-0">
        {/* The list comes from the server already ranked; cmdk must not re-filter it */}
        <Command shouldFilter={false}>
          <CommandInput
            value={query}
            onValueChange={handleInput}
            placeholder={t.form.searchInstrumentPlaceholder}
            className="text-base md:text-sm"
          />
          <CommandList>
            {loading && mainItems.length === 0 && recentItems.length === 0 ? (
              <div className="py-1" role="status">
                <span className="sr-only">{t.common.loading}</span>
                {Array.from({ length: 5 }).map((_, i) => (
                  <div key={i} className="flex h-9 items-center justify-between px-2">
                    <Skeleton className="h-4 w-32 max-w-[60%]" />
                    <Skeleton className="h-5 w-5 rounded-full flex-shrink-0" />
                  </div>
                ))}
              </div>
            ) : (
              <CommandEmpty>{t.rules.noInstrumentsFound}</CommandEmpty>
            )}
            {recentItems.length > 0 && (
              <CommandGroup heading={t.rules.recentInstruments}>
                {recentItems.map(renderItem)}
              </CommandGroup>
            )}
            <CommandGroup>{mainItems.map(renderItem)}</CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
