import { useState, useEffect, useCallback, useMemo, useRef, memo } from 'react';
import { useNavigate, useParams, useSearchParams, useNavigationType } from 'react-router-dom';
import { AnimatePresence, motion, useReducedMotion } from 'motion/react';
import { useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useListPainted } from '@/hooks/useListPainted';
import { useToast } from '@/hooks/use-toast';
import { useTranslation } from '@/i18n';
import { getIndicatorShortName } from '@/i18n/indicatorTranslations';
import { api, account } from '@/services/api';
import { isSubNew } from '@/lib/alertRead';
import { qk, apiErrorText } from '@/lib/query';
import { countApiLeafConditions, primaryApiCondition } from '@/components/RuleEditor/schema';
import type { SubscriptionDto, SubscriptionListFilters, SubscriptionsSort } from '@/types/contracts';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { AlertDialog, AlertDialogContent, AlertDialogHeader, AlertDialogFooter, AlertDialogTitle, AlertDialogDescription, AlertDialogAction, AlertDialogCancel } from '@/components/ui/alert-dialog';
import { ContextMenu, ContextMenuTrigger, ContextMenuContent, ContextMenuItem, ContextMenuSeparator } from '@/components/ui/context-menu';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus, Loader2, ChevronRight, Bell, ListChecks, BellPlus, Trash2, AlertTriangle, Power, PowerOff, Search, CheckCheck, ArrowUpDown, SlidersHorizontal } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { PaginationControls, getPageSize } from '@/components/pagination-controls';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InstrumentLogo } from '@/components/InstrumentLogo';
import { formatCooldownSeconds } from '@/lib/cooldown';
import { formatPlural, pluralForm } from '@/lib/plural';
import { dashboardPrefix } from '@/lib/paths';
import { statusView } from '@/lib/limits';
import { useKeyboardOpen } from '@/hooks/useKeyboardOpen';
import { SubscriptionFilters, hasSubscriptionFilters } from '@/components/SubscriptionFilters';
import { FirstRunNotice } from '@/components/FirstRunNotice';
import type { SubscriptionFilterValue } from '@/components/SubscriptionFilters';

// --- Alert item row ---
const AlertItem = memo(function AlertItem({ sub, onView, onToggleActive, onDelete, t, index }: {
  sub: SubscriptionDto;
  onView: (sub: SubscriptionDto) => void;
  onToggleActive: (sub: SubscriptionDto) => void;
  onDelete: (sub: SubscriptionDto) => void;
  t: ReturnType<typeof useTranslation>['t'];
  index: number;
}) {
  const conditionCount = sub.rule ? countApiLeafConditions(sub.rule.when) : 1;
  const status = statusView(sub.status, t);
  const statusLabel = status.label;
  const indicatorName = getIndicatorShortName(primaryApiCondition(sub.rule)?.indicator ?? '');
  const cooldownLabel = formatCooldownSeconds(sub.cooldownSeconds, t);
  // The menu item names the alert by the client-given rule name (the list row
  // already shows the cooldown); legacy rules without a name fall back to the indicator
  const deleteLabel = t.subscriptions.contextMenu.deleteAlert
    .replace('{name}', sub.rule?.name?.trim() || indicatorName);
  return (
    <>
      {index > 0 && <div className="h-px bg-border mx-2" />}
      <ContextMenu>
        <ContextMenuTrigger asChild onContextMenu={event => event.stopPropagation()}>
          <button
            type="button"
            className="group w-full text-left px-2 py-1.5 rounded-md transition-colors cursor-pointer hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            onClick={() => onView(sub)}
            aria-label={`${indicatorName}, ${cooldownLabel}, ${statusLabel}`}
          >
            {/* Mobile: the rule description gets its own full-width line - on a
                single row the shrink-0 badges starved the truncate down to 0px.
                Row 1: indicator + condition count + status; row 2: description + cooldown */}
            <span className="flex items-center gap-2 md:hidden">
              <Badge variant="secondary" className="min-w-0 max-w-[180px] truncate">{indicatorName}</Badge>
              <Badge variant="outline" className="flex-shrink-0 text-[10px]">
                <ListChecks className="h-3 w-3 mr-1" />{conditionCount}
              </Badge>
              <span className="ml-auto flex items-center gap-1 flex-shrink-0">
                {isSubNew(sub) && <Badge variant="default" className="text-[10px]">{t.subscriptions.grouped.new}</Badge>}
                <Badge variant={status.variant} className={cn('text-[10px]', status.className)}>
                  {statusLabel}
                </Badge>
              </span>
            </span>
            <span className="mt-0.5 flex items-start gap-2 md:hidden">
              {sub.rule?.name && (
                <span className="text-xs text-muted-foreground min-w-0 flex-1 line-clamp-2">{sub.rule.name}</span>
              )}
              <span className="ml-auto text-xs text-muted-foreground flex-shrink-0 whitespace-nowrap">
                {cooldownLabel}
              </span>
            </span>
            {/* Desktop: the original single-line row */}
            <span className="hidden md:flex md:items-center md:justify-between">
              <span className="flex items-center gap-2 min-w-0 flex-1">
                <Badge variant="secondary" className="flex-shrink min-w-0 truncate max-w-[240px]" title={indicatorName}>{indicatorName}</Badge>
                {sub.rule?.name && (
                  <span className="text-xs text-muted-foreground min-w-0 truncate" title={sub.rule.name}>{sub.rule.name}</span>
                )}
                <span className="text-xs text-muted-foreground flex-shrink-0">
                  {cooldownLabel}
                </span>
                <Badge variant="outline" className="flex-shrink-0 text-[10px]">
                  <ListChecks className="h-3 w-3 mr-1" />{conditionCount}
                </Badge>
              </span>
              <span className="flex items-center gap-1 flex-shrink-0 ml-1.5">
                {isSubNew(sub) && <Badge variant="default" className="text-[10px]">{t.subscriptions.grouped.new}</Badge>}
                <Badge variant={status.variant} className={cn('text-[10px]', status.className)}>
                  {statusLabel}
                </Badge>
              </span>
            </span>
          </button>
        </ContextMenuTrigger>
        <ContextMenuContent>
          <ContextMenuItem onSelect={() => onToggleActive(sub)}>
            {sub.status === 'Active' ? <PowerOff /> : <Power />}
            {sub.status === 'Active' ? t.subscriptions.contextMenu.disable : t.subscriptions.contextMenu.enable}
          </ContextMenuItem>
          <ContextMenuSeparator />
          <ContextMenuItem variant="destructive" onSelect={() => onDelete(sub)}>
            <Trash2 />
            {deleteLabel}
          </ContextMenuItem>
        </ContextMenuContent>
      </ContextMenu>
    </>
  );
});

// --- Ticker group card ---
const TickerGroupCard = memo(function TickerGroupCard({ ticker, exchange, alerts, onView, t, locale, expanded, cardRef, onToggleGroup, onAddAlert, onDeleteGroup, onToggleActive, onDeleteSub }: {
  ticker: string;
  exchange: string | null;
  alerts: SubscriptionDto[];
  onView: (sub: SubscriptionDto) => void;
  t: ReturnType<typeof useTranslation>['t'];
  locale: string;
  expanded: boolean;
  cardRef?: (el: HTMLDivElement | null) => void;
  onToggleGroup: (group: TickerGroup) => void;
  onAddAlert: (group: TickerGroup) => void;
  onDeleteGroup: (group: TickerGroup) => void;
  onToggleActive: (sub: SubscriptionDto) => void;
  onDeleteSub: (sub: SubscriptionDto) => void;
}) {
  const alertCount = alerts.length;
  const alertLabel = formatPlural(locale, alertCount, t.subscriptions.grouped.alertsCount);
  const groupLabel = exchange ? `${ticker} \u00b7 ${exchange}` : ticker;
  const reduceMotion = useReducedMotion();
  // Group identity for the stable parent callbacks (groupKeyOf is hoisted)
  const group: TickerGroup = { key: groupKeyOf(exchange, ticker), ticker, exchange, subs: alerts };

  return (
    <ContextMenu>
      <ContextMenuTrigger asChild>
        <Card ref={cardRef}>
          <CardContent className="p-0">
            <button
              onClick={() => onToggleGroup(group)}
              className={cn(
                'w-full flex items-center justify-between p-3 md:p-4 text-left transition-colors rounded-t-lg',
                expanded ? 'bg-muted/30' : 'hover:bg-muted/30'
              )}
            >
              {/* min-w-0 + truncating code chip: long tickers used to push the
                  exchange logo out of the card (nothing in the row could shrink) */}
              <div className="flex items-center gap-3 min-w-0 flex-1">
                <ChevronRight className={cn('h-4 w-4 flex-shrink-0 text-muted-foreground transition-transform duration-200', expanded && 'rotate-90')} />
                <InstrumentLogo ticker={ticker} exchangeName={exchange} size="xs" />
                <code className="text-sm font-mono font-semibold bg-muted px-2 py-1 rounded min-w-0 truncate">{ticker}</code>
                {exchange && (
                  <ExchangeLogo name={exchange} />
                )}
              </div>
              <span className="flex items-center gap-1.5 flex-shrink-0">
                {alerts.some(isSubNew) && <Badge variant="default" className="text-[10px]">{t.subscriptions.grouped.new}</Badge>}
                <Badge variant="secondary" className="text-[10px]">
                  <Bell className="h-3 w-3 mr-1" />{alertLabel}
                </Badge>
              </span>
            </button>

            <AnimatePresence initial={false}>
              {expanded && (
                <motion.div
                  key="alerts"
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  transition={{ duration: reduceMotion ? 0 : 0.2, ease: 'easeInOut' }}
                  className="overflow-hidden"
                >
                  <div className="px-1.5 py-1.5 border-t">
                    {alerts.map((sub, idx) => (
                      <AlertItem
                        key={sub.id}
                        sub={sub}
                        onView={onView}
                        onToggleActive={onToggleActive}
                        onDelete={onDeleteSub}
                        t={t}
                        index={idx}
                      />
                    ))}
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </CardContent>
        </Card>
      </ContextMenuTrigger>
      <ContextMenuContent>
        <ContextMenuItem onSelect={() => onAddAlert(group)}>
          <BellPlus />
          {t.subscriptions.contextMenu.addAlert}
        </ContextMenuItem>
        <ContextMenuSeparator />
        <ContextMenuItem variant="destructive" onSelect={() => onDeleteGroup(group)}>
          <Trash2 />
          {t.subscriptions.contextMenu.delete.replace('{ticker}', groupLabel)}
        </ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  );
});

// --- "Mark all as read": icon-only button — the hint bubble shows on hover on the
// desktop; Radix never opens tooltips on touch, so a tap keeps it open for a moment
// (the action fires right away — the bubble only names what the tap did) ---
const MarkAllReadButton = memo(function MarkAllReadButton({ onMarkAllRead, hasUnread, pending, side, t, className }: {
  onMarkAllRead: () => void;
  hasUnread: boolean;
  pending: boolean;
  side: 'top' | 'bottom';
  t: ReturnType<typeof useTranslation>['t'];
  className?: string;
}) {
  const [hintOpen, setHintOpen] = useState(false);
  const hintTimer = useRef<number | null>(null);

  const showTouchHint = () => {
    if (hintTimer.current) window.clearTimeout(hintTimer.current);
    setHintOpen(true);
    hintTimer.current = window.setTimeout(() => {
      hintTimer.current = null;
      setHintOpen(false);
    }, 2000);
  };

  useEffect(() => () => {
    if (hintTimer.current) window.clearTimeout(hintTimer.current);
  }, []);

  return (
    <TooltipProvider delayDuration={100}>
      <Tooltip
        open={hintOpen}
        // while the touch hint runs Radix's own close (pointerup/leave) must not kill it
        onOpenChange={(next) => { if (!next && hintTimer.current) return; setHintOpen(next); }}
      >
        <TooltipTrigger asChild>
          <Button
            variant="outline"
            size="icon"
            className={className}
            disabled={!hasUnread || pending}
            aria-label={t.subscriptions.markAllRead.tooltip}
            onPointerDown={(e) => { if (e.pointerType === 'touch') showTouchHint(); }}
            onClick={onMarkAllRead}
          >
            {pending ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCheck className="h-4 w-4" />}
          </Button>
        </TooltipTrigger>
        <TooltipContent side={side}>{t.subscriptions.markAllRead.tooltip}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
});

// --- refs map for scroll-into-view ---
function useCardRefs() {
  const refs = useRef<Record<string, HTMLDivElement | null>>({});
  // Per-key ref callbacks kept stable across renders so memoized cards can receive them
  const refCallbacks = useRef<Map<string, (el: HTMLDivElement | null) => void>>(new Map());

  const setRefFor = useCallback((key: string) => {
    let callback = refCallbacks.current.get(key);
    if (!callback) {
      callback = (el: HTMLDivElement | null) => {
        refs.current[key] = el;
      };
      refCallbacks.current.set(key, callback);
    }
    return callback;
  }, []);

  const getEl = useCallback((key: string) => {
    return refs.current[key] ?? null;
  }, []);

  return useMemo(() => ({ setRefFor, getEl }), [setRefFor, getEl]);
}

interface TickerGroup {
  key: string;
  ticker: string;
  exchange: string | null;
  subs: SubscriptionDto[];
  /** Newest LastTriggeredAt among the group's subscriptions (null — none ever fired). */
  lastAlertAt?: string | null;
}

// Group identity: exchange + ticker, the same ticker may trade on multiple exchanges
function groupKeyOf(exchange: string | null, ticker: string): string {
  return `${exchange ?? ''}|${ticker}`;
}

// The exchanges the instrument providers can name; the popover also unions in whatever
// shows up in the user's own subscriptions (a legacy rule may watch an older exchange)
const KNOWN_EXCHANGES = ['BINANCE', 'CRYPTO', 'MOEX', 'NASDAQ', 'NYSE', 'NYSE MKT', 'ARCA', 'BATS', 'AMEX', 'IEX', 'OTC'];

export default function SubscriptionsPage() {
  const { loaded, isAuthenticated, profile, refresh } = useAuth();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { t, locale } = useTranslation();
  // retracts the mobile action bar while the soft keyboard is up (globals in the hook)
  const keyboardOpen = useKeyboardOpen();
  const navigate = useNavigate();
  const params = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigationType = useNavigationType();
  const prefix = dashboardPrefix(locale);
  const baseRoute = `${prefix}/subscriptions`;

  const tickerFromUrl = params.ticker ?? null;
  const exchangeFromUrl = searchParams.get('ex');

  // The URL is the single source of truth for the search term and the chip filters
  // (the instruments-page pattern): the server filters the list, so reloads and
  // Back/Forward restore the exact view; the input chases the URL after a POP.
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

  const search = searchParams.get('q') ?? '';
  const statuses = useMemo(
    () => (searchParams.get('status') ?? '').split(',').filter(Boolean),
    [searchParams],
  );
  const filters = useMemo<SubscriptionFilterValue>(() => ({
    statuses,
    type: searchParams.get('type') ?? '',
    indicator: searchParams.get('indicator') ?? '',
    exchange: searchParams.get('exchange') ?? '',
    triggered: searchParams.get('trig') === '1',
  }), [searchParams, statuses]);

  const listFilters = useMemo<SubscriptionListFilters>(() => ({
    q: search || undefined,
    statuses: filters.statuses.length > 0 ? filters.statuses : undefined,
    type: filters.type || undefined,
    indicator: filters.indicator || undefined,
    exchange: filters.exchange || undefined,
    triggered: filters.triggered || undefined,
  }), [search, filters]);

  const filtersActive = search !== '' || hasSubscriptionFilters(filters);

  // The chip row sits behind an icon button; opening is pure UI state (not a URL
  // param), seeded open when the page was entered through a filtered URL
  const [filtersOpen, setFiltersOpen] = useState(() => hasSubscriptionFilters(filters));
  const activeFilterCount =
    (filters.statuses.length > 0 ? 1 : 0) +
    (filters.type ? 1 : 0) +
    (filters.indicator ? 1 : 0) +
    (filters.exchange ? 1 : 0) +
    (filters.triggered ? 1 : 0);

  // Seed from the module-level cache so a revisit renders instantly and the API
  // revalidates silently in the background (stale-while-revalidate)
  // The shared RQ cache (30 s staleTime) replaces the old module-level cache:
  // a revisit within the window renders instantly without a request
  const subsQuery = useQuery({
    queryKey: qk.subscriptionsList(
      search, filters.statuses.join(','), filters.type, filters.indicator, filters.exchange, filters.triggered ? '1' : '',
    ),
    queryFn: () => api.getAllSubscriptions(listFilters),
    // Switching a chip/page keys a fresh query: without the previous list as
    // placeholder the skeleton would flash over the list on every filter change
    placeholderData: keepPreviousData,
    enabled: loaded && isAuthenticated,
    // Returning to the tab must refresh the list even while the data is still
    // within the 30 s staleTime (the global default keeps focus refetches off)
    refetchOnWindowFocus: 'always',
    // New alerts roll in while the page is being looked at; RQ pauses the interval
    // in background tabs, so a hidden tab costs no requests
    refetchInterval: 30_000,
  });
  const subscriptions = useMemo(() => subsQuery.data ?? [], [subsQuery.data]);
  const loading = subsQuery.isPending;
  // with cached data a failed background refetch must not drop the UI — keep the last good list
  const loadError = subsQuery.error && !subsQuery.data ? apiErrorText(subsQuery.error, t) : null;
  const [deleteGroup, setDeleteGroup] = useState<TickerGroup | null>(null);
  const [deleteSub, setDeleteSub] = useState<SubscriptionDto | null>(null);

  const rawPage = Number.parseInt(searchParams.get('page') ?? '1', 10);
  const page = Number.isFinite(rawPage) && rawPage > 1 ? rawPage : 1;
  const setPage = useCallback(
    (value: number) => updateParams((sp) => {
      if (value > 1) sp.set('page', String(value));
      else sp.delete('page');
    }, { replace: true }),
    [updateParams],
  );

  // Debounced search input: the URL chases the input (350 ms, the instruments-page
  // recipe), and a new term always restarts the list from the first page
  const [inputValue, setInputValue] = useState(search);
  const isFirstRender = useRef(true);

  useEffect(() => {
    if (navigationType === 'POP') setInputValue(search);
  }, [navigationType, search]);

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    const id = setTimeout(() => {
      updateParams((sp) => {
        const term = inputValue.trim();
        if (term) sp.set('q', term);
        else sp.delete('q');
        sp.delete('page');
      }, { replace: true });
    }, 350);
    return () => clearTimeout(id);
  }, [inputValue, updateParams]);

  // Every filter change resets pagination (the term does it inside its debounce)
  const handleFiltersChange = useCallback((mutate: (v: SubscriptionFilterValue) => SubscriptionFilterValue) => {
    updateParams((sp) => {
      const next = mutate(filters);

      if (next.statuses.length > 0) sp.set('status', next.statuses.join(','));
      else sp.delete('status');

      for (const key of ['type', 'indicator', 'exchange'] as const)
        if (next[key]) sp.set(key, next[key]);
        else sp.delete(key);

      if (next.triggered) sp.set('trig', '1');
      else sp.delete('trig');

      sp.delete('page');
    }, { replace: true });
  }, [filters, updateParams]);

  const clearFilters = useCallback(() => {
    updateParams((sp) => {
      for (const key of ['status', 'type', 'indicator', 'exchange', 'trig', 'page'])
        sp.delete(key);
    }, { replace: true });
  }, [updateParams]);

  // Exchanges offered by the filter: the providers' set plus whatever the user's
  // own subscriptions carry (legacy rules may name an older exchange)
  const exchanges = useMemo(() => {
    const seen = new Set(KNOWN_EXCHANGES);
    for (const sub of subscriptions) {
      const ex = primaryApiCondition(sub.rule)?.exchangeName;
      if (ex) seen.add(ex);
    }
    return [...seen].sort((a, b) => a.localeCompare(b));
  }, [subscriptions]);

  // The order lives in the user profile (PATCH /account/preferences); the local
  // override keeps the select instant while the PATCH + profile refresh are in flight
  const [sortPref, setSortPref] = useState<SubscriptionsSort | null>(null);
  const [sortSaving, setSortSaving] = useState(false);
  const sort = sortPref ?? profile?.subscriptionsSort ?? 'alertDate';

  const handleSortChange = useCallback(async (value: SubscriptionsSort) => {
    if (value === sort) return;
    setSortPref(value);
    setSortSaving(true);
    try {
      await account.updatePreferences({ subscriptionsSort: value });
      await refresh();
    } catch {
      toast({ title: t.errors.title, description: t.errors.failedToSave, variant: 'destructive' });
    } finally {
      setSortSaving(false);
    }
  }, [sort, refresh, toast, t]);

  const cardRefs = useCardRefs();
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const expandedPageKeyRef = useRef<string | null>(null);
  // Half of the instruments-page density: groups carry a whole list of alerts each
  // Page counted in ticker groups; a collapsed group is a single row, so the
  // old mobile floor of 5 left most of the screen empty (10 fits comfortably)
  const pageSize = getPageSize(10, 15);

  // Grouping of the (already server-filtered) list: the search and the chips run in
  // SQL over the instruments/indicators, only the grouping and the profile sort are here
  const groups = useMemo(() => {
    const map = new Map<string, TickerGroup>();
    for (const sub of subscriptions) {
      const primary = primaryApiCondition(sub.rule);
      const ticker = primary?.ticker ?? '-';
      const exchange = primary?.exchangeName ?? null;
      const key = groupKeyOf(exchange, ticker);
      const group = map.get(key);
      if (group) group.subs.push(sub);
      else map.set(key, { key, ticker, exchange, subs: [sub] });
    }
    const built = [...map.values()].map(g => {
      const subs = g.subs.sort((a, b) => b.created.localeCompare(a.created));
      const lastAlertAt = subs.reduce<string | null>(
        (latest, sub) => (sub.lastTriggeredAt && (!latest || sub.lastTriggeredAt > latest) ? sub.lastTriggeredAt : latest),
        null);
      return { ...g, subs, lastAlertAt };
    });

    const byTicker = (a: TickerGroup, b: TickerGroup) =>
      a.ticker.localeCompare(b.ticker) || (a.exchange ?? '').localeCompare(b.exchange ?? '');

    // The order the user picked in the select (stored in the profile)
    if (sort === 'alertCount')
      built.sort((a, b) => b.subs.length - a.subs.length || (b.lastAlertAt ?? '').localeCompare(a.lastAlertAt ?? '') || byTicker(a, b));
    else if (sort === 'ticker')
      built.sort(byTicker);
    else if (sort === 'created')
      // subs are already created-desc, so subs[0] is the freshest of the group
      built.sort((a, b) => b.subs[0].created.localeCompare(a.subs[0].created) || byTicker(a, b));
    else
      // alertDate: freshest alert first (never-fired groups sink to the bottom)
      built.sort((a, b) => (b.lastAlertAt ?? '').localeCompare(a.lastAlertAt ?? '') || b.subs.length - a.subs.length || byTicker(a, b));

    return built;
  }, [subscriptions, sort]);

  const totalPages = Math.max(1, Math.ceil(groups.length / pageSize));
  const pageGroups = useMemo(
    () => groups.slice((page - 1) * pageSize, page * pageSize),
    [groups, page, pageSize]
  );

  // A group card carries a whole list of alerts, so a fresh commit of up to 15 of them
  // paints progressively and reads as the list populating after the skeleton goes away;
  // the gate keeps the skeleton over the invisible groups until the first frame is
  // drawn, then hands off with a short fade (see useListPainted). Keyed on the cache
  // entry, so client-side search, sort and paging stay instant.
  const reduceMotion = useReducedMotion();
  const { painted: listPainted, busy: listBusy } = useListPainted(subsQuery.data, pageGroups.length);

  const findGroupByTicker = useCallback((ticker: string, exchange: string | null) => {
    if (exchange) {
      const exact = groups.find(g => g.ticker === ticker && g.exchange === exchange);
      if (exact) return exact;
    }
    return groups.find(g => g.ticker === ticker) ?? null;
  }, [groups]);

  /** Updates every list variant (all filter combinations) in the shared cache so the
   *  change survives remounts of this page. */
  const applySubsUpdate = useCallback((updater: (prev: SubscriptionDto[]) => SubscriptionDto[]) => {
    queryClient.setQueriesData<SubscriptionDto[]>({ queryKey: qk.subscriptions }, (prev) => (prev ? updater(prev) : prev));
  }, [queryClient]);

  /** Drops the per-subscription entries (detail/history) of removed subscriptions. */
  const dropSubEntries = useCallback((ids: Iterable<string>) => {
    for (const id of ids) {
      queryClient.removeQueries({ queryKey: qk.subscription(id) });
      queryClient.removeQueries({ queryKey: qk.triggerHistory(id) });
    }
  }, [queryClient]);

  // --- the URL is the single source of truth for the expanded group ---
  // (covers deep links, toggle, and "Subscriptions" nav clicks clearing the route)
  const expandedKey = useMemo(() => {
    if (!tickerFromUrl) return null;
    return findGroupByTicker(tickerFromUrl, exchangeFromUrl)?.key ?? null;
  }, [tickerFromUrl, exchangeFromUrl, findGroupByTicker]);

  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages, setPage]);

  useEffect(() => {
    if (!expandedKey) {
      expandedPageKeyRef.current = null;
      return;
    }
    if (expandedPageKeyRef.current === expandedKey) return;
    expandedPageKeyRef.current = expandedKey;
    const idx = groups.findIndex(g => g.key === expandedKey);
    if (idx < 0) return;
    const target = Math.floor(idx / pageSize) + 1;
    if (target !== page) setPage(target);
  }, [expandedKey, groups, pageSize, page, setPage]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: 0 });
  }, [page]);

  // --- scroll into view when the URL ticker or expanded group changes ---
  useEffect(() => {
    if (!tickerFromUrl || !expandedKey) return;
    const group = groups.find(g => g.key === expandedKey);
    if (!group) return;
    const el = cardRefs.getEl(group.key);
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [tickerFromUrl, expandedKey, groups, cardRefs, page]);

  // --- handlers ---
  // Expand/collapse rewrites only the path segment and ?ex= - the search and chip
  // params of the current view ride along, so collapsing a group never resets filters
  const handleToggleGroup = useCallback((group: TickerGroup) => {
    if (expandedKey === group.key) {
      const sp = new URLSearchParams(searchParams);
      sp.delete('ex');
      const qs = sp.toString();
      navigate(`${baseRoute}${qs ? `?${qs}` : ''}`, { replace: false });
    } else {
      const sp = new URLSearchParams(searchParams);
      sp.delete('ex');
      if (group.exchange) sp.set('ex', group.exchange);
      const qs = sp.toString();
      navigate(`${baseRoute}/${group.ticker}${qs ? `?${qs}` : ''}`, { replace: true });
    }
  }, [expandedKey, navigate, baseRoute, searchParams]);

  const handleNew = useCallback(() => {
    navigate(`${baseRoute}/new`);
  }, [navigate, baseRoute]);

  const [markAllPending, setMarkAllPending] = useState(false);
  const hasUnreadSubs = useMemo(() => subscriptions.some(isSubNew), [subscriptions]);

  // One bulk POST clears every unread flag, then the cached list is stamped read
  const handleMarkAllRead = useCallback(async () => {
    if (!hasUnreadSubs || markAllPending) return;
    setMarkAllPending(true);
    try {
      await api.markAllSubscriptionsRead();
      applySubsUpdate(prev => prev.map(s => (isSubNew(s) ? { ...s, lastReadAt: s.lastTriggeredAt } : s)));
      toast({ title: t.subscriptions.markAllRead.success, variant: 'success', duration: 2000 });
    } catch {
      toast({ title: t.errors.title, description: t.errors.failedToMarkAllRead, variant: 'destructive' });
    } finally {
      setMarkAllPending(false);
    }
  }, [hasUnreadSubs, markAllPending, toast, t, applySubsUpdate]);

  const handleView = useCallback((sub: SubscriptionDto) => {
    const ticker = primaryApiCondition(sub.rule)?.ticker ?? '-';
    // No ?ex= here: the detail page takes the exchange from the subscription's rule
    // itself - the parameter only disambiguates the expanded group in the list
    navigate(`${baseRoute}/${ticker}/${sub.id}`);

    // Opening the alert clears its "New" flag: optimistic cache sync first, then the
    // idempotent POST /read (a lost request just brings the flag back on the next load)
    if (isSubNew(sub)) {
      const readAt = sub.lastTriggeredAt;
      applySubsUpdate(prev => prev.map(s => (s.id === sub.id ? { ...s, lastReadAt: readAt } : s)));
      queryClient.setQueryData<SubscriptionDto>(qk.subscription(sub.id),
        (prev) => (prev ? { ...prev, lastReadAt: readAt } : prev));
      void api.markSubscriptionRead(sub.id).catch(() => {});
    }
  }, [navigate, baseRoute, applySubsUpdate, queryClient]);

  const handleAddAlert = useCallback((group: TickerGroup) => {
    const qs = `ticker=${encodeURIComponent(group.ticker)}${group.exchange ? `&exchange=${encodeURIComponent(group.exchange)}` : ''}&from=subscriptions`;
    navigate(`${baseRoute}/new?${qs}`);
  }, [navigate, baseRoute]);

  const handleConfirmDeleteGroup = useCallback(async () => {
    const group = deleteGroup;
    if (!group) return;
    const groupLabel = group.exchange ? `${group.ticker} \u00b7 ${group.exchange}` : group.ticker;
    setDeleteGroup(null);
    try {
      await api.deleteInstrumentSubscriptions(group.ticker, group.exchange);
      const removedIds = new Set(group.subs.map(sub => sub.id));
      applySubsUpdate(prev => prev.filter(sub => !removedIds.has(sub.id)));
      dropSubEntries(removedIds);
      if (expandedKey === group.key) {
        navigate(baseRoute);
      }
      toast({
        title: t.subscriptions.deleteGroup.success.replace('{ticker}', groupLabel),
        variant: 'success',
        duration: 2000,
      });
    } catch {
      toast({ title: t.errors.title, description: t.errors.failedToDelete, variant: 'destructive' });
    }
  }, [deleteGroup, expandedKey, navigate, baseRoute, toast, t, applySubsUpdate, dropSubEntries]);

  const handleToggleActive = useCallback(async (sub: SubscriptionDto) => {
    const nextActive = sub.status !== 'Active';
    try {
      await api.setSubscriptionActive(sub.id, nextActive);
      const nextStatus = nextActive ? 'Active' as const : 'Inactive' as const;
      applySubsUpdate(prev => prev.map(s => (s.id === sub.id ? { ...s, status: nextStatus } : s)));
      queryClient.setQueryData<SubscriptionDto>(qk.subscription(sub.id),
        (prev) => (prev ? { ...prev, status: nextStatus } : prev));
      toast({
        title: nextActive ? t.subscriptions.toggle.enabled : t.subscriptions.toggle.disabled,
        variant: 'success',
        duration: 2000,
      });
    } catch {
      toast({ title: t.errors.title, description: t.errors.failedToToggle, variant: 'destructive' });
    }
  }, [toast, t, applySubsUpdate, queryClient]);

  const handleConfirmDeleteSub = useCallback(async () => {
    const sub = deleteSub;
    if (!sub) return;
    setDeleteSub(null);
    try {
      await api.deleteSubscription(sub.id);
      applySubsUpdate(prev => prev.filter(s => s.id !== sub.id));
      dropSubEntries([sub.id]);
      toast({ title: t.subscriptions.delete.success, variant: 'success', duration: 2000 });
    } catch {
      toast({ title: t.errors.title, description: t.errors.failedToDelete, variant: 'destructive' });
    }
  }, [deleteSub, toast, t, applySubsUpdate, dropSubEntries]);

  const deleteGroupLabel = deleteGroup
    ? deleteGroup.exchange ? `${deleteGroup.ticker} \u00b7 ${deleteGroup.exchange}` : deleteGroup.ticker
    : '';

  // While auth is unresolved (or anonymous, when MainLayout redirects to login)
  // render nothing — the fetch above still runs in parallel, so authenticated
  // loads lose no time, and anonymous first visits never flash the page spinner.
  if (!loaded || !isAuthenticated) return null;

  // The loading skeleton and the paint-gate overlay share this exact layout so the
  // handoff between them does not shift the scroller
  const skeletonRows = (
    <>
      <span className="sr-only">{t.common.loading}</span>
      {Array.from({ length: 4 }).map((_, i) => (
        <Card key={i}>
          <CardContent className="p-3 md:p-4 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <Skeleton className="h-4 w-4" />
              <Skeleton className="h-7 w-20" />
              <Skeleton className="h-5 w-5 rounded-full" />
            </div>
            <Skeleton className="h-5 w-16 rounded-full flex-shrink-0" />
          </CardContent>
        </Card>
      ))}
    </>
  );

  return (
    <div className="flex flex-col h-[var(--app-page-h-bar)] md:h-[var(--app-page-h)]">
      <div className="flex items-start justify-between">
        <h1 className="text-xl md:text-2xl font-bold pb-3">{t.subscriptions.title}</h1>
        {/* Always rendered (even while loading / on the empty state) so it never
            jumps between the header and the content when the list finishes loading */}
        <div className="hidden md:flex items-center gap-2">
          <MarkAllReadButton onMarkAllRead={handleMarkAllRead} hasUnread={hasUnreadSubs} pending={markAllPending} side="bottom" t={t} />
          <Button size="sm" onClick={handleNew} disabled={!!loadError}>
            <Plus className="h-4 w-4" />
            <span>{t.common.createSubscription}</span>
          </Button>
        </div>
      </div>

      <FirstRunNotice />

      {/* Pinned outside the scroll container: an overflow ancestor clips the focus ring of an input flush with its edges.
          Stays visible while search/filters are active even when they match nothing, so the chips can be cleared */}
      {(subscriptions.length > 0 || filtersActive) && (
        <div className="mt-3">
          {/* One row from sm up; on mobile the search takes the whole first row
              and the sort + filter wrap to a second one, otherwise the sort's
              shrink-0 label squeezes the search field away */}
          <div className="flex flex-wrap items-center gap-2">
            <form onSubmit={(e) => e.preventDefault()} className="relative min-w-0 flex-1 basis-full sm:basis-auto">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                placeholder={t.subscriptions.searchPlaceholder}
                className="pl-9"
                aria-label={t.subscriptions.searchPlaceholder}
              />
            </form>
            <Select value={sort} onValueChange={(value) => void handleSortChange(value as SubscriptionsSort)}>
              <SelectTrigger className="w-auto min-w-0 flex-1 sm:flex-none sm:shrink-0 gap-1.5" aria-label={t.subscriptions.sort.aria} disabled={sortSaving}>
                <ArrowUpDown className="h-4 w-4 shrink-0 text-muted-foreground" />
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="alertDate">{t.subscriptions.sort.alertDate}</SelectItem>
                <SelectItem value="alertCount">{t.subscriptions.sort.alertCount}</SelectItem>
                <SelectItem value="ticker">{t.subscriptions.sort.ticker}</SelectItem>
                <SelectItem value="created">{t.subscriptions.sort.created}</SelectItem>
              </SelectContent>
            </Select>
            <Button
              variant="outline"
              size="icon"
              className="relative h-10 w-10 shrink-0"
              aria-label={t.subscriptions.filters.aria}
              aria-expanded={filtersOpen}
              onClick={() => setFiltersOpen((open) => !open)}
            >
              <SlidersHorizontal className="h-4 w-4" />
              {activeFilterCount > 0 && (
                <Badge className="absolute -top-1 -right-1 h-4 min-w-4 px-1 text-[10px]">{activeFilterCount}</Badge>
              )}
            </Button>
          </div>

          <AnimatePresence initial={false}>
            {filtersOpen && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                transition={{ duration: reduceMotion ? 0 : 0.2, ease: 'easeInOut' }}
                className="overflow-hidden"
              >
                <SubscriptionFilters
                  value={filters}
                  exchanges={exchanges}
                  onChange={handleFiltersChange}
                  onClear={clearFilters}
                  t={t}
                />
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      <div ref={scrollRef} className="relative mt-3 -mx-1.5 flex-1 min-h-0 overflow-y-auto px-1.5 pb-2">
        {loadError ? (
          <Alert variant="destructive">
            <AlertTitle>{t.errors.title}</AlertTitle>
            <AlertDescription>{loadError}</AlertDescription>
          </Alert>
        ) : (
          <div className="relative">
            {!loading && !listBusy && subscriptions.length === 0 && !filtersActive && (
              <div className="text-center py-12">
                <p className="text-muted-foreground mb-2">{t.subscriptions.noSubscriptions}</p>
                <p className="text-sm text-muted-foreground">{t.subscriptions.noSubscriptionsHint}</p>
              </div>
            )}

            {!loading && !listBusy && subscriptions.length === 0 && filtersActive && (
              <p className="text-center py-12 text-muted-foreground">{t.subscriptions.noResults}</p>
            )}

            {pageGroups.length > 0 && (
              <div className={cn('space-y-3', !reduceMotion && 'transition-opacity duration-150', listPainted ? 'opacity-100' : 'opacity-0')} aria-hidden={!listPainted}>
                {pageGroups.map(group => (
                  <TickerGroupCard
                    key={group.key}
                    ticker={group.ticker}
                    exchange={group.exchange}
                    alerts={group.subs}
                    onView={handleView}
                    t={t}
                    locale={locale}
                    expanded={expandedKey === group.key}
                    onToggleGroup={handleToggleGroup}
                    cardRef={cardRefs.setRefFor(group.key)}
                    onAddAlert={handleAddAlert}
                    onDeleteGroup={setDeleteGroup}
                    onToggleActive={handleToggleActive}
                    onDeleteSub={setDeleteSub}
                  />
                ))}
              </div>
            )}

            {(loading || listBusy) && (
              <div className={cn('space-y-3', listBusy && 'absolute inset-0 z-10')} role="status">
                {skeletonRows}
              </div>
            )}
          </div>
        )}
      </div>

      {!loading && !listBusy && !loadError && groups.length > 0 && totalPages > 1 && (
        <PaginationControls page={page} totalPages={totalPages} onPageChange={setPage} />
      )}

      {/* Mobile sticky bottom actions; hidden while the soft keyboard is up (it would
          sit behind the keys; --bar-h collapses in index.css so the list reclaims it) */}
      {!keyboardOpen && (
        <div className="md:hidden fixed bottom-[var(--nav-h)] left-0 right-0 z-30 p-3 bg-background/95 backdrop-blur border-t">
          <div className="flex items-center gap-2">
            <MarkAllReadButton onMarkAllRead={handleMarkAllRead} hasUnread={hasUnreadSubs} pending={markAllPending} side="top" t={t} className="h-11 w-11 shrink-0" />
            <Button className="flex-1 h-11" onClick={handleNew} disabled={!!loadError}>
              <Plus className="h-4 w-4" />
              {t.common.createSubscription}
            </Button>
          </div>
        </div>
      )}

      <AlertDialog open={deleteGroup != null} onOpenChange={open => { if (!open) setDeleteGroup(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-destructive flex-shrink-0" />
              {t.subscriptions.deleteGroup.title}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {deleteGroup
                && pluralForm(locale, deleteGroup.subs.length, t.subscriptions.deleteGroup.message)
                  .replace('{{count}}', String(deleteGroup.subs.length))
                  .replace('<code>', '')
                  .replace('{ticker}', deleteGroupLabel)
                  .replace('</code>', '')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t.common.cancel}</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={handleConfirmDeleteGroup}>
              {t.common.delete}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>


      <AlertDialog open={deleteSub != null} onOpenChange={open => { if (!open) setDeleteSub(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-destructive flex-shrink-0" />
              {t.subscriptions.deleteAlert.title}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {deleteSub
                && t.subscriptions.deleteAlert.message
                  .replace('<code>', '')
                  .replace('{indicator}', getIndicatorShortName(primaryApiCondition(deleteSub.rule)?.indicator ?? ''))
                  .replace('{cooldown}', formatCooldownSeconds(deleteSub.cooldownSeconds, t))
                  .replace('</code>', '')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t.common.cancel}</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={handleConfirmDeleteSub}>
              {t.common.delete}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
