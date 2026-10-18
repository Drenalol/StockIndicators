import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useNavigate, useSearchParams, useNavigationType } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AnimatePresence, motion, useReducedMotion } from 'motion/react';
import { useAuth } from '@/hooks/useAuth';
import { useListPainted } from '@/hooks/useListPainted';
import { useTickerLiveUpdates } from '@/hooks/useTickerLiveUpdates';
import { useTranslation } from '@/i18n';
import { api } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import type { ChartCandleData, InstrumentDto } from '@/types/contracts';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { InteractiveChart } from '@/components/InteractiveChart';
import { ChartExtendedLine } from '@/components/ChartExtendedLine';
import {
  Select, SelectTrigger, SelectValue, SelectContent, SelectItem,
} from '@/components/ui/select';
import { ArrowDownRight, ArrowUpRight, Search, ArrowLeft, Plus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { PaginationControls, getPageSize } from '@/components/pagination-controls';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InstrumentLogo } from '@/components/InstrumentLogo';
import { dashboardPrefix } from '@/lib/paths';
import { onExternalLinkClick } from '@/lib/externalLink';

const TYPES = ['All', 'Stock', 'Bond', 'Etf', 'Option', 'Future', 'Crypto'] as const;

export default function InstrumentsPage() {
  const { loaded, isAuthenticated } = useAuth();
  const { t, locale } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const reduceMotion = useReducedMotion();

  const authed = loaded && isAuthenticated;

  // The URL is the single source of truth for the list/detail state (B9: raw
  // pushState/popstate sync wiped React Router's history state, so browser
  // back reopened the card). The detail is an in-page view addressed by
  // ?ticker(+?exch) pushed on top of the list params; filters/page/search
  // live in the URL through replace-navigations.
  const [searchParams, setSearchParams] = useSearchParams();
  const navigationType = useNavigationType();

  const updateParams = useCallback(
    (mutate: (sp: URLSearchParams) => void, options?: { replace?: boolean }) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        mutate(next);
        return next;
      }, options);
    },
    [setSearchParams],
  );

  const debouncedSearch = searchParams.get('search') ?? '';
  const typeFilter = searchParams.get('type') ?? 'All';
  const rawPage = Number.parseInt(searchParams.get('page') ?? '1', 10);
  const page = Number.isFinite(rawPage) && rawPage > 1 ? rawPage : 1;
  const tickerParam = searchParams.get('ticker');
  const selected = tickerParam ? { ticker: tickerParam, exchange: searchParams.get('exch') } : null;

  // --- Debounced search ---
  const isFirstRender = useRef(true);

  // --- List state ---
  const [inputValue, setInputValue] = useState(() => debouncedSearch);
  const pageSize = getPageSize();

  // --- Detail state ---
  // The shared cache keeps the visited pages/details: paging back and forth and
  // returning from a detail view is served without re-downloading
  const listQuery = useQuery({
    queryKey: qk.instruments(page, pageSize, debouncedSearch, typeFilter),
    queryFn: () => api.getAllInstruments(
      { page, pageSize, search: debouncedSearch || undefined, type: typeFilter !== 'All' ? typeFilter : undefined },
    ),
    enabled: authed && !selected,
  });
  const instruments = useMemo(() => listQuery.data?.items ?? [], [listQuery.data]);
  const totalCount = listQuery.data?.totalCount ?? 0;
  const loading = authed && !selected ? listQuery.isPending : false;
  const listError = listQuery.error ? apiErrorText(listQuery.error, t) : null;

  // A freshly committed 30-row list paints progressively inside the scroller (the browser
  // fills the layer top to bottom), so dropping the skeleton the moment data arrives made
  // users watch the table "populate". The gate keeps the skeleton over the invisible rows
  // until the first frame is drawn, then hands off with a short fade (see useListPainted).
  const { painted, busy } = useListPainted(listQuery.data, instruments.length);
  const listBusy = loading || busy;

  const detailQuery = useQuery({
    queryKey: qk.instrument(selected?.ticker ?? '', selected?.exchange),
    queryFn: () => api.getInstrument(selected!.ticker, selected!.exchange),
    enabled: authed && !!selected,
  });
  const detailData = detailQuery.data && detailQuery.data.ticker ? detailQuery.data : null;
  const detailLoading = !!selected && detailQuery.isPending;
  const detailError = detailQuery.error
    ? apiErrorText(detailQuery.error, t)
    : (detailQuery.data && !detailQuery.data.ticker ? t.instruments.notFound : null);

  // Plain price preview of the selected instrument (daily candles, no indicators).
  // A 404 (no candles in cache for this instrument) simply renders no chart.
  const chartQuery = useQuery({
    queryKey: qk.instrumentChart(selected?.ticker ?? '', selected?.exchange),
    queryFn: () => api.getInstrumentChart(selected!.ticker, selected!.exchange),
    enabled: authed && !!selected,
  });

  const chartData = chartQuery.data ?? null;

  const selectedTickerKey = selected ? `${selected.ticker}|${selected.exchange ?? ''}` : '';

  const handleLiveTick = useCallback(() => {
    const [ticker, exchange] = selectedTickerKey.split('|');
    queryClient.invalidateQueries({
      queryKey: qk.instrumentChart(ticker, exchange === '' ? null : exchange),
      refetchType: 'active',
    });
  }, [queryClient, selectedTickerKey]);

  // The instrument detail is the "chart is open" state: live nudges (and the hub
  // connection itself) exist only while it is on screen
  useTickerLiveUpdates(
    selected ? [{ ticker: selected.ticker, exchange: selected.exchange }] : [],
    handleLiveTick,
    authed && !!selected,
  );

  // The search box mirrors the URL term after a browser Back/Forward (POP);
  // while the user is typing the URL chases the input (debounced below)
  useEffect(() => {
    if (navigationType === 'POP') setInputValue(debouncedSearch);
  }, [navigationType, debouncedSearch]);

  // --- Debounce search input (350ms), reset page on change ---
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    const id = setTimeout(() => {
      updateParams((sp) => {
        const term = inputValue.trim();
        if (term) sp.set('search', term);
        else sp.delete('search');
        sp.delete('page');
      }, { replace: true });
    }, 350);
    return () => clearTimeout(id);
  }, [inputValue, updateParams]);

  // --- Handlers ---
  const handleSelect = useCallback((inst: InstrumentDto) => {
    // Opening the detail pushes a history entry, so browser back closes it
    updateParams((sp) => {
      sp.set('ticker', inst.ticker);
      if (inst.exchangeName) sp.set('exch', inst.exchangeName);
      else sp.delete('exch');
    });
  }, [updateParams]);

  const handleCreateAlert = useCallback(() => {
    if (!detailData) return;
    const sp = new URLSearchParams();
    sp.set('ticker', detailData.ticker);
    if (detailData.exchangeName) sp.set('exchange', detailData.exchangeName);
    navigate(`${dashboardPrefix(locale)}/subscriptions/new?${sp.toString()}`);
  }, [detailData, locale, navigate]);

  // The detail is an in-page view, so the arrow closes it in place with
  // replace: rewriting the current entry (instead of popping) keeps the
  // latest filters in the URL and leaves no opened-card entry behind that
  // a later browser back could reopen (that was B9)
  const handleBack = useCallback(() => {
    updateParams((sp) => {
      sp.delete('ticker');
      sp.delete('exch');
    }, { replace: true });
  }, [updateParams]);

  const handleSearchSubmit = useCallback((e: React.FormEvent) => {
    e.preventDefault();
  }, []);

  const handleTypeChange = useCallback((val: string) => {
    updateParams((sp) => {
      if (val === 'All') sp.delete('type');
      else sp.set('type', val);
      sp.delete('page');
    }, { replace: true });
  }, [updateParams]);

  const handlePageChange = useCallback((nextPage: number) => {
    updateParams((sp) => {
      if (nextPage > 1) sp.set('page', String(nextPage));
      else sp.delete('page');
    }, { replace: true });
  }, [updateParams]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Blank while auth is unresolved or anonymous (MainLayout redirects to login);
  // the fetches above run in parallel, so authenticated loads lose no time
  if (!loaded || !isAuthenticated) return null;

  return (
    <div className="space-y-6">
      {/* ===== LIST / DETAIL VIEWS ===== */}
      {/* Fixed viewport-fit height from the chrome tokens (--app-page-h = header + border + main paddings, see index.css) */}
      {/* Fade-only switch between the views, the same feel as the subscriptions accordion */}
      <AnimatePresence mode="wait" initial={false}>
      {!selected && (
      <motion.div
        key="list"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: reduceMotion ? 0 : 0.2, ease: 'easeInOut' }}
        className="flex flex-col h-[var(--app-page-h)]"
      >
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
          <h1 className="text-xl md:text-2xl font-bold pb-3">{t.instruments.title}</h1>
        </div>

        <div className="mt-3 flex flex-col sm:flex-row gap-3">
          <form onSubmit={handleSearchSubmit} className="relative flex-1 min-w-0">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder={t.instruments.searchPlaceholder}
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              className="pl-9"
            />
          </form>
          <Select value={typeFilter} onValueChange={handleTypeChange}>
            <SelectTrigger className="w-full sm:w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {TYPES.map((tp) => (
                <SelectItem key={tp} value={tp}>
                  {t.instruments[`type_${tp.toLowerCase()}` as keyof typeof t.instruments] ?? tp}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Results count */}
        {!listBusy && (
          <p className="mt-3 text-xs text-muted-foreground pt-2 pb-2">
            {totalCount > 0
              ? `${(page - 1) * pageSize + 1}-${Math.min(page * pageSize, totalCount)} / ${totalCount}`
              : t.instruments.noInstruments}
          </p>
        )}

        <div className="relative mt-3 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2">
          {listError ? (
            <Alert variant="destructive"><AlertDescription>{listError}</AlertDescription></Alert>
          ) : (
            <div className="relative">
              {instruments.length > 0 && (
                <div className={cn('grid grid-cols-1 gap-2', !reduceMotion && 'transition-opacity duration-150', painted ? 'opacity-100' : 'opacity-0')} aria-hidden={!painted}>
                  {instruments.map((inst) => (
                    <Card key={`${inst.ticker}|${inst.exchangeName ?? ''}`} className="cursor-pointer transition-colors hover:bg-muted/50">
                      <CardContent className="flex items-center justify-between p-3 md:p-4" onClick={() => handleSelect(inst)}>
                        <div className="flex items-center gap-2 md:gap-3 min-w-0">
                          <InstrumentLogo ticker={inst.ticker} exchangeName={inst.exchangeName} />
                          <code className="text-xs md:text-sm bg-muted px-1.5 py-1 rounded flex-shrink-0 font-mono font-semibold">{inst.ticker}</code>
                          {inst.exchangeName && <ExchangeLogo name={inst.exchangeName} />}
                          {inst.name && <span className="text-xs text-muted-foreground truncate hidden md:inline">{inst.name}</span>}
                          {inst.type && <Badge variant="outline" className="text-[10px] hidden sm:inline-flex">{inst.type}</Badge>}
                        </div>
                        <div className="flex items-center gap-2 flex-shrink-0 text-xs text-muted-foreground">
                          {inst.currency}
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
              {listBusy && (
                <div className={cn('grid grid-cols-1 gap-2', busy && 'absolute inset-0 z-10')} role="status">
                  <span className="sr-only">{t.common.loading}</span>
                  {Array.from({ length: 6 }).map((_, i) => (
                    <Card key={i}>
                      <CardContent className="flex items-center justify-between p-3 md:p-4">
                        <div className="flex items-center gap-2 md:gap-3">
                          <Skeleton className="h-7 w-7 rounded-lg flex-shrink-0" />
                          <Skeleton className="h-6 w-16 flex-shrink-0" />
                          <Skeleton className="h-5 w-5 rounded-full" />
                        </div>
                        <Skeleton className="h-4 w-8 flex-shrink-0" />
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
              {!listBusy && instruments.length === 0 && (
                <div className="h-full flex items-center justify-center text-muted-foreground">
                  {inputValue ? t.instruments.noResults : t.instruments.noInstruments}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Pagination */}
        {!listBusy && !listError && instruments.length > 0 && totalPages > 1 && (
          <PaginationControls page={page} totalPages={totalPages} onPageChange={handlePageChange} />
        )}
      </motion.div>
      )}

      {/* ===== DETAIL VIEW ===== */}
      {selected && (
      <motion.div
        key="detail"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: reduceMotion ? 0 : 0.2, ease: 'easeInOut' }}
        className="flex flex-col h-[var(--app-page-h)]"
      >
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="icon" onClick={handleBack}>
              <ArrowLeft className="h-5 w-5" />
            </Button>
            <h1 className="text-xl md:text-2xl font-bold flex items-center gap-2 min-w-0">
              <InstrumentLogo ticker={selected.ticker} exchangeName={selected.exchange} size="xs" />
              <code className="bg-muted px-2 py-1 rounded text-sm font-semibold">{selected.ticker}</code>
              {selected.exchange && (
                <ExchangeLogo name={selected.exchange} />
              )}
              {detailData?.type && <Badge variant="outline" className="flex-shrink-0 text-[10px]">{detailData.type}</Badge>}
            </h1>
          </div>

          <div className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-6">
            {/* Price preview (quote + daily candles) loads independently of the meta */}
            {chartQuery.isPending ? (
              <div role="status" className="space-y-3">
                <span className="sr-only">{t.common.loading}</span>
                <div className="flex items-baseline gap-3">
                  <Skeleton className="h-9 w-36" />
                  <Skeleton className="h-5 w-24" />
                </div>
                <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
              </div>
            ) : chartData && chartData.candles.length >= 2 ? (
              <>
                <div className="space-y-1.5">
                  <InstrumentQuote candles={chartData.candles} currency={detailData?.currency} locale={locale} />
                  {/* TradingView-style: the extended minutes already merged into the live
                      forming bar, this line carries its price and the session name */}
                  <ChartExtendedLine
                    extended={chartData.extended}
                    formatPrice={(v) => formatQuotePrice(v, detailData?.currency, locale)}
                    className="w-full"
                  />
                </div>
                <InteractiveChart data={chartData} />
              </>
            ) : null}

            {detailLoading ? (
              <div className="space-y-6" role="status">
                <span className="sr-only">{t.common.loading}</span>
                <Card>
                  <CardContent className="p-4 space-y-3">
                    {Array.from({ length: 7 }).map((_, i) => (
                      <div key={i} className="flex items-center justify-between gap-3">
                        <Skeleton className="h-4 w-20 flex-shrink-0" />
                        <Skeleton className="h-4 w-28 md:w-40" />
                      </div>
                    ))}
                  </CardContent>
                </Card>
                <div className="flex justify-end">
                  <Skeleton className="h-11 w-full sm:w-48" />
                </div>
              </div>
            ) : detailError || !detailData ? (
              <Alert variant="destructive">
                <AlertDescription>{detailError ?? t.instruments.notFound}</AlertDescription>
              </Alert>
            ) : (
              <>
                <Card>
                  <CardContent className="p-4 space-y-3">
                    {(() => {
                      const rows: { label: string; value?: string | number | null; wrap?: boolean }[] = [
                        { label: t.instruments.name, value: detailData.longName || detailData.name, wrap: true },
                        { label: t.instruments.sector, value: detailData.sector },
                        { label: t.instruments.industry, value: detailData.industry },
                        { label: t.instruments.country, value: detailData.country },
                        { label: t.instruments.isin, value: detailData.isin },
                        { label: t.instruments.classCode, value: detailData.classCode },
                        { label: t.instruments.employees, value: detailData.employees },
                        { label: t.instruments.website, value: detailData.website },
                        { label: t.instruments.currency, value: detailData.currency },
                      ].filter(r => r.value != null && r.value !== '');
                      return rows.map((r) => (
                        <div key={r.label} className={cn('flex justify-between gap-3', r.wrap ? 'flex-wrap items-start gap-y-1' : 'items-center')}>
                          <span className="text-sm text-muted-foreground flex-shrink-0">{r.label}</span>
                          {typeof r.value === 'string' && /^https?:\/\//.test(r.value) ? (
                            <a href={r.value} target="_blank" rel="noreferrer" onClick={onExternalLinkClick} className="text-sm truncate text-primary hover:underline">{r.value}</a>
                          ) : (
                            <span className={cn('text-sm text-right', r.wrap ? 'min-w-0 flex-1 basis-[calc(100%-110px)] break-words' : 'truncate')}>{r.value}</span>
                          )}
                        </div>
                      ));
                    })()}
                    {detailData.summary && (
                      <p className="text-sm text-muted-foreground border-t pt-3">{detailData.summary}</p>
                    )}
                  </CardContent>
                </Card>

                <div className="flex justify-end">
                  <Button className="w-full sm:w-auto" onClick={handleCreateAlert}>
                    <Plus className="h-4 w-4 mr-2" />
                    {t.common.createSubscription}
                  </Button>
                </div>
              </>
            )}
          </div>
      </motion.div>
      )}
      </AnimatePresence>
    </div>
  );
}

function formatQuotePrice(value: number, currency: string | null | undefined, locale: string): string {
  const digits = Math.abs(value) < 1 ? 6 : Math.abs(value) < 10 ? 4 : 2;

  if (currency) {
    try {
      return new Intl.NumberFormat(locale, { style: 'currency', currency, maximumFractionDigits: digits }).format(value);
    } catch {
      // an unknown currency code still deserves a readable number, just without the symbol
    }
  }

  const plain = value.toLocaleString(locale, { maximumFractionDigits: digits });

  return currency ? `${plain} ${currency}` : plain;
}

/**
 * The quote line above the preview chart: the last close and the change to the
 * previous daily close. Both numbers are derived from the chart candles themselves
 * (the backend already re-stamps the window to the user's timezone), so no extra
 * quote endpoint exists; the "exchange closed" state is simply an unchanged pair.
 */
function InstrumentQuote({ candles, currency, locale }: { candles: ChartCandleData[]; currency: string | null | undefined; locale: string }) {
  const last = candles[candles.length - 1];
  const prev = candles[candles.length - 2];
  const change = last.close - prev.close;
  const pct = prev.close !== 0 ? (change / prev.close) * 100 : 0;
  const up = change > 0;
  const down = change < 0;

  const pctText = new Intl.NumberFormat(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Math.abs(pct));
  const sign = up ? '+' : down ? '-' : '';

  return (
    <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
      <span className="text-3xl font-semibold tabular-nums leading-none">
        {formatQuotePrice(last.close, currency, locale)}
      </span>
      <span
        className={cn(
          'inline-flex items-center gap-1 text-sm font-medium tabular-nums',
          up ? 'text-success' : down ? 'text-destructive' : 'text-muted-foreground',
        )}
      >
        {up ? <ArrowUpRight className="h-4 w-4" /> : down ? <ArrowDownRight className="h-4 w-4" /> : null}
        {`${sign}${formatQuotePrice(Math.abs(change), currency, locale)}`}
        <span className="text-muted-foreground">{`(${sign}${pctText}%)`}</span>
      </span>
    </div>
  );
}
