import { useState, useEffect, useMemo, useRef } from 'react';
import { useNavigate, useParams, useSearchParams, Link } from 'react-router-dom';
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { useToast } from '@/hooks/use-toast';
import { getIndicatorFullNameWithAbbr } from '@/i18n/indicatorTranslations';
import { api, account } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { isSubNew } from '@/lib/alertRead';
import { statusView } from '@/lib/limits';
import { countApiLeafConditions, collectApiConditions, primaryApiCondition } from '@/components/RuleEditor/schema';
import { RuleGroupView } from '@/components/RuleEditor/RuleGroupView';
import type { SubscriptionDto, SubscriptionOutputDto, TriggerHistoryDto } from '@/types/contracts';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InstrumentLogo } from '@/components/InstrumentLogo';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { TriggerDeliveries } from '@/components/TriggerDeliveries';
import { channelLabel } from '@/lib/alertChannels';
import { AlertDialog, AlertDialogContent, AlertDialogHeader, AlertDialogFooter, AlertDialogTitle, AlertDialogDescription, AlertDialogAction, AlertDialogCancel } from '@/components/ui/alert-dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { ArrowLeft, ListChecks, Edit, Trash2, AlertTriangle, Power, PowerOff, ChevronRight, LineChart, MoreHorizontal } from 'lucide-react';
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { formatDate, formatTriggerDate } from '@/lib/datetime';
import { formatCooldownSeconds } from '@/lib/cooldown';
import { dashboardPrefix } from '@/lib/paths';

const TRIGGER_HISTORY_PAGE_SIZE = 10;
const TRIGGER_HISTORY_SERVER_LIMIT = 100;
const TRIGGER_HISTORY_PRELOAD_MARGIN = 200;

export default function SubscriptionDetailPage() {
  const params = useParams();
  const subId = params.subId!;
  const navigate = useNavigate();
  const { loaded, isAuthenticated } = useAuth();
  const { t, locale } = useTranslation();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const goBackOr = useGoBackOr();
  const prefix = dashboardPrefix(locale);

  const authed = loaded && isAuthenticated;

  const subscriptionQuery = useQuery({
    queryKey: qk.subscription(subId),
    queryFn: () => api.getSubscription(subId),
    enabled: authed,
  });
  const subscription = subscriptionQuery.data ?? null;
  const loading = subscriptionQuery.isPending;
  // The subscription carries no instrument/indicator of its own — the "primary"
  // rendering identity is the first leaf condition of its rule
  const primary = subscription ? primaryApiCondition(subscription.rule) : null;

  // Opening the subscription (from the list or a deep link, e.g. a Telegram alert's
  // "history" button) clears its "New" flag: stamp the caches right away, then fire
  // the idempotent POST /read (a lost request just brings the flag back later)
  const markedReadRef = useRef(false);
  useEffect(() => {
    if (!subscription || markedReadRef.current || !isSubNew(subscription)) return;
    markedReadRef.current = true;
    const readAt = subscription.lastTriggeredAt;
    queryClient.setQueryData<SubscriptionDto>(qk.subscription(subId),
      (prev) => (prev ? { ...prev, lastReadAt: readAt } : prev));
    queryClient.setQueriesData<SubscriptionDto[]>({ queryKey: qk.subscriptions },
      (prev) => (prev ? prev.map((s) => (s.id === subId ? { ...s, lastReadAt: readAt } : s)) : prev));
    void api.markSubscriptionRead(subId).catch(() => {});
  }, [subscription, subId, queryClient]);

  const historyQuery = useQuery({
    queryKey: qk.triggerHistory(subId),
    queryFn: () => api.getTriggerHistory(subId),
    enabled: authed,
  });
  const historyData = historyQuery.data;
  // silent — history may not exist yet (or the lookup failed): render an empty list
  const triggerHistory = useMemo(() => historyData ?? [], [historyData]);
  const historyLoading = historyQuery.isPending;
  const [historyVisibleCount, setHistoryVisibleCount] = useState(TRIGGER_HISTORY_PAGE_SIZE);
  const historySentinelRef = useRef<HTMLDivElement | null>(null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const historySectionRef = useRef<HTMLDivElement | null>(null);

  // Deep-link landing (?view=history from the alert channels' "history" buttons):
  // scroll the trigger history into view once, after the page has rendered
  const [searchParams] = useSearchParams();
  const scrollToHistory = useRef(searchParams.get('view') === 'history');
  useEffect(() => {
    if (!scrollToHistory.current || loading) return;
    scrollToHistory.current = false;
    requestAnimationFrame(() => historySectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }, [loading]);

  // restart the client-side pagination whenever a fresh history chunk arrives
  useEffect(() => {
    if (historyData) setHistoryVisibleCount(TRIGGER_HISTORY_PAGE_SIZE);
  }, [historyData]);

  // Deliverability check: the channels configured on this subscription may point to
  // resources the user no longer has (unlinked Telegram chat, deleted push subscription).
  // A failed lookup leaves the state null — better to hide the warning than to lie.
  // Gated on the subscription: the check is meaningless before it has loaded.
  const chatsQuery = useQuery({
    queryKey: qk.telegramChats,
    queryFn: () => account.getTelegramChats(),
    enabled: authed && !!subscription,
  });
  const pushQuery = useQuery({
    queryKey: qk.pushSubscriptions,
    queryFn: () => api.getPushSubscriptions(),
    enabled: authed && !!subscription,
  });
  const userChats = chatsQuery.data ?? null;
  const pushSubscriptions = pushQuery.data ?? null;

  const [deleteOpen, setDeleteOpen] = useState(false);

  // Lazy pagination: append the next chunk when the sentinel below the table
  // gets close to the bottom of the page's scroll container. A scroll listener
  // (plus an immediate check on every append) is used instead of
  // IntersectionObserver so fast jumps (scrollbar drag, End key) that skip
  // past the sentinel still trigger it. `loading` is in the deps so the
  // listener (re)registers once the scroll container has mounted.
  useEffect(() => {
    const el = historySentinelRef.current;
    const scroller = scrollRef.current;
    if (!el || !scroller || historyVisibleCount >= triggerHistory.length) return;
    const check = () => {
      if (el.getBoundingClientRect().top < window.innerHeight + TRIGGER_HISTORY_PRELOAD_MARGIN) {
        setHistoryVisibleCount((c) => Math.min(c + TRIGGER_HISTORY_PAGE_SIZE, triggerHistory.length));
      }
    };
    check();
    scroller.addEventListener('scroll', check, { passive: true });
    return () => scroller.removeEventListener('scroll', check);
  }, [loading, historyLoading, historyVisibleCount, triggerHistory.length]);

  // Localized "what does this parameter mean" hints. A rule can mix several
  // indicators (SMA + Gap + ...), so the template descriptions are loaded for
  // every indicator present in the rule, keyed by indicator name.
  const indicatorKey = useMemo(() => {
    const set = new Set<string>();
    if (subscription?.rule) {
      for (const c of collectApiConditions(subscription.rule.when)) set.add(c.indicator);
    }
    return [...set].sort().join(',');
  }, [subscription]);

  const indicators = useMemo(() => (indicatorKey ? indicatorKey.split(',') : []), [indicatorKey]);
  const templateQueries = useQueries({
    queries: indicators.map((indicator) => ({
      queryKey: qk.indicatorTemplate(indicator, locale),
      queryFn: () => api.getIndicatorTemplate(indicator, locale),
      enabled: authed,
    })),
  });
  // a failed lookup degrades to no hints for that indicator
  const parameterHintsByIndicator = useMemo(
    () => Object.fromEntries(indicators.map((indicator, i) => [indicator, templateQueries[i]?.data?.descriptions ?? {}])),
    [indicators, templateQueries],
  );

  // null — unknown (still loading or lookup failed); otherwise the reason why no
  // enabled output of this subscription can deliver alerts
  const undeliverable = useMemo<'nothingConfigured' | 'noChannels' | null>(() => {
    if (!subscription || userChats === null || pushSubscriptions === null) return null;
    const outputs = (subscription.outputs ?? []).filter((o) => o.isEnabled);
    if (outputs.length === 0) return 'noChannels';

    const isDeliverable = (o: SubscriptionOutputDto) => {
      if (o.channel === 'Telegram') {
        // An account has at most one chat and a stale output addressee is
        // rerouted to it at delivery time - a linked chat is all it takes
        return userChats.length > 0;
      }
      if (o.channel === 'WebPush') {
        return o.pushSubscriptionId != null && pushSubscriptions.some((p) => p.id === o.pushSubscriptionId);
      }
      // Webhook/Discord/WhatsApp deliver as soon as a destination is filled in
      if (o.channel === 'Webhook' || o.channel === 'Discord' || o.channel === 'WhatsApp') {
        return o.target != null && o.target !== '';
      }
      return false;
    };

    return outputs.some(isDeliverable) ? null : 'nothingConfigured';
  }, [subscription, userChats, pushSubscriptions]);

  const handleEdit = () => {
    const ticker = primary?.ticker ?? '-';
    navigate(`${prefix}/subscriptions/${ticker}/${subId}/edit`);
  };

  // The chart opened in a dialog before; it is a full page now so it gets its
  // own URL and the native/Android back returns to this subscription
  const openTriggerPage = (entry: TriggerHistoryDto) => {
    navigate(`${prefix}/subscriptions/${primary?.ticker ?? params.ticker}/${subId}/triggers/${entry.id}`);
  };

  const listUrlFor = (ticker: string | null | undefined, exchange: string | null) =>
    ticker
      ? `${prefix}/subscriptions/${ticker}${exchange ? `?ex=${encodeURIComponent(exchange)}` : ''}`
      : `${prefix}/subscriptions`;

  // Usually entered from the (expanded) subscriptions list, so the arrow pops
  // history there; the list URL with the expanded group is the deep-link fallback
  const handleBack = () => {
    const ticker = primary?.ticker ?? params.ticker;
    goBackOr(listUrlFor(ticker, primary?.exchangeName ?? null));
  };

  const handleDeleteConfirm = () => {
    setDeleteOpen(true);
  };

  const deleteMutation = useMutation({
    mutationFn: () => api.deleteSubscription(subId),
  });

  const handleDelete = () => {
    if (!subscription) return;
    const savedTicker = primary?.ticker;
    const savedExchange = primary?.exchangeName ?? null;
    deleteMutation.mutate(undefined, {
      onSuccess: () => {
        // keep the cached list truthful and drop the deleted subscription's own entries
        queryClient.setQueriesData<SubscriptionDto[]>({ queryKey: qk.subscriptions },
          (prev) => (prev ? prev.filter((s) => s.id !== subId) : prev));
        queryClient.removeQueries({ queryKey: qk.subscription(subId) });
        queryClient.removeQueries({ queryKey: qk.triggerHistory(subId) });
        setDeleteOpen(false);
        navigate(listUrlFor(savedTicker, savedExchange));
        toast({
          title: t.subscriptions.delete.success,
          variant: 'success',
          duration: 2000,
        });
      },
      onError: () => {
        setDeleteOpen(false);
        toast({ title: t.errors.title, description: t.errors.failedToDelete, variant: 'destructive' });
      },
    });
  };

  // Same mechanism as the subscriptions-list context menu: POST /active, then update the cache
  const toggleActiveMutation = useMutation({
    mutationFn: (nextActive: boolean) => api.setSubscriptionActive(subId, nextActive),
  });

  const handleToggleActive = () => {
    if (!subscription) return;
    const nextActive = subscription.status !== 'Active';
    toggleActiveMutation.mutate(nextActive, {
      onSuccess: () => {
        const nextStatus = nextActive ? 'Active' as const : 'Inactive' as const;
        queryClient.setQueryData<SubscriptionDto>(qk.subscription(subId),
          (prev) => (prev ? { ...prev, status: nextStatus } : prev));
        queryClient.setQueriesData<SubscriptionDto[]>({ queryKey: qk.subscriptions },
          (prev) => (prev ? prev.map((s) => (s.id === subId ? { ...s, status: nextStatus } : s)) : prev));
        toast({
          title: nextActive ? t.subscriptions.toggle.enabled : t.subscriptions.toggle.disabled,
          variant: 'success',
          duration: 2000,
        });
      },
      onError: () => {
        toast({ title: t.errors.title, description: t.errors.failedToToggle, variant: 'destructive' });
      },
    });
  };

  // Blank while auth is unresolved or anonymous (MainLayout redirects to login);
  // the fetches above run in parallel, so authenticated loads lose no time
  if (!loaded || !isAuthenticated) return null;

  if (loading) {
    return (
      <div className="flex flex-col h-[var(--app-page-h)]" role="status">
        <span className="sr-only">{t.common.loading}</span>
        <div className="space-y-2">
          <div className="flex items-center gap-3">
            <Skeleton className="h-10 w-10 rounded-md flex-shrink-0" />
            <Skeleton className="h-7 w-24" />
            <div className="ml-auto flex items-center gap-2 shrink-0">
              <Skeleton className="h-10 w-16 rounded-md" />
              <Skeleton className="h-10 w-10 rounded-md" />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Skeleton className="h-7 w-28 rounded-full" />
            <Skeleton className="h-6 w-10 rounded-full" />
          </div>
        </div>
        <div className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-6">
          <div className="rounded-lg border p-4">
            <div className="grid grid-cols-3 gap-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="space-y-1.5">
                  <Skeleton className="h-3 w-16" />
                  <Skeleton className="h-4 w-12" />
                </div>
              ))}
            </div>
          </div>
          <div className="rounded-lg border bg-muted/30 p-4">
            <Skeleton className="h-3 w-24" />
            <div className="mt-3 space-y-2">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-5/6" />
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (subscriptionQuery.error || !subscription) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={handleBack}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <h1 className="text-xl md:text-2xl font-bold">{t.subscriptions.title}</h1>
        </div>
        <p className="text-muted-foreground">{subscriptionQuery.error ? apiErrorText(subscriptionQuery.error, t) : t.errors.failedToLoad}</p>
      </div>
    );
  }

  const conditionCount = subscription.rule ? countApiLeafConditions(subscription.rule.when) : 1;
  const status = statusView(subscription.status, t);

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height — the page must never scroll the browser window (see AGENTS.md) */}
      {/* Header: back + ticker + indicator */}
      <div className="space-y-2">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={handleBack}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          {/* The action buttons are fixed-size, so the ticker block is the one that
              shrinks: the wordmark does not fit next to the icon + ticker + the
              edit/overflow pair on a phone-width header (it used to paint under
              them); it continues on the badges line below instead */}
          <h1 className="text-xl md:text-2xl font-bold flex items-center gap-2 min-w-0 overflow-hidden">
            <InstrumentLogo ticker={primary?.ticker ?? ''} exchangeName={primary?.exchangeName} size="xs" />
            <code className="bg-muted px-2 py-1 rounded text-sm font-semibold truncate">{primary?.ticker ?? '-'}</code>
          </h1>
          {/* SaaS overflow-menu pattern: the primary action (edit) stays visible,
              the secondary actions live in the right-most [ ⋯ ] menu */}
          <div className="ml-auto flex items-center gap-2 shrink-0">
            <Button variant="outline" onClick={handleEdit} className="px-3 sm:px-4">
              <Edit className="h-4 w-4 mr-0 sm:mr-1.5" />
              <span className="hidden sm:inline">{t.common.edit}</span>
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="icon" aria-label={t.subscriptions.moreActions}>
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="min-w-44">
                <DropdownMenuItem
                  onSelect={() => navigate(`${prefix}/subscriptions/${encodeURIComponent(primary?.ticker ?? params.ticker ?? '')}/${subId}/chart`)}
                >
                  <LineChart className="text-muted-foreground" />
                  {t.subscriptions.snapshot.open}
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={handleToggleActive}>
                  {subscription.status === 'Active'
                    ? <PowerOff className="text-muted-foreground" />
                    : <Power className="text-primary" />}
                  {subscription.status === 'Active' ? t.subscriptions.contextMenu.disable : t.subscriptions.contextMenu.enable}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onSelect={handleDeleteConfirm}>
                  <Trash2 />
                  {t.common.delete}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {primary?.exchangeName && (
            <ExchangeLogo name={primary.exchangeName} />
          )}
          {primary?.indicator && (
            <Link
              to={`${prefix}/indicators/${encodeURIComponent(primary.indicator)}`}
              title={t.indicators.whatIsIt}
              className="rounded-full transition-colors hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <Badge variant="secondary" className="py-1.5">{getIndicatorFullNameWithAbbr(primary.indicator, t)}</Badge>
            </Link>
          )}
          <Badge variant="outline"><ListChecks className="h-3 w-3 mr-1" />{conditionCount}</Badge>
        </div>
      </div>

      <div ref={scrollRef} className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-6">
        {/* Deliverability warning: at least one enabled output has nowhere to deliver to */}
        {undeliverable && (
          <Alert variant="warning">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>{t.notifications.noOutputs.title}</AlertTitle>
            <AlertDescription>
              {undeliverable === 'noChannels'
                ? t.notifications.noOutputs.noChannels
                : t.notifications.noOutputs.nothingConfigured}{' '}
              <Link
                to={undeliverable === 'noChannels'
                  ? `${prefix}/subscriptions/${encodeURIComponent(primary?.ticker ?? '')}/${subId}/edit`
                  : `${prefix}/settings?tab=providers`}
                className="font-semibold"
              >
                {undeliverable === 'noChannels' ? t.notifications.noOutputs.editLink : t.notifications.noOutputs.settingsLink}
              </Link>
            </AlertDescription>
          </Alert>
        )}

        {/* Details */}
        <div className="rounded-lg border p-4">
          <div className="grid grid-cols-3 gap-4">
            <div>
              <Label className="text-xs text-muted-foreground">{t.subscriptions.alert.cooldown}</Label>
              <p className="text-sm font-medium mt-0.5">{formatCooldownSeconds(subscription.cooldownSeconds, t)}</p>
            </div>
            <div>
              <Label className="text-xs text-muted-foreground">{t.subscriptions.alert.status}</Label>
              <div className="mt-0.5">
                <Badge variant={status.variant} className={status.className}>
                  {status.label}
                </Badge>
              </div>
            </div>
            <div>
              <Label className="text-xs text-muted-foreground">{t.subscriptions.alert.created}</Label>
              <p className="text-sm font-medium mt-0.5">{formatDate(subscription.created, locale)}</p>
            </div>
          </div>
        </div>

        {/* Alert channels */}
        {(subscription.outputs ?? []).length > 0 && (
          <div className="rounded-lg border p-4">
            <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">{t.notifications.title}</Label>
            <div className="flex flex-wrap gap-2 mt-2">
              {subscription.outputs.map((o) => (
                <Badge key={o.id} variant="outline">
                  {channelLabel(o.channel, t)}
                </Badge>
              ))}
            </div>
          </div>
        )}

        {/* Condition */}
        <div className="rounded-lg border bg-muted/30 p-4">
          <div className="flex items-center justify-between mb-2">
            <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
              {t.subscriptions.alert.conditionDescription}
            </Label>
            <Badge variant="outline" className="text-[10px]">
              <ListChecks className="h-3 w-3 mr-1" />{conditionCount}
            </Badge>
          </div>
          {subscription.rule?.name && (
            <p className="text-sm font-medium mb-2 truncate" title={subscription.rule.name}>{subscription.rule.name}</p>
          )}
          {subscription.rule && (
            <RuleGroupView group={subscription.rule.when} parameterHintsByIndicator={parameterHintsByIndicator} t={t} />
          )}
        </div>

        {/* Trigger History */}
        <div ref={historySectionRef} className="rounded-lg border bg-muted/30 p-4 scroll-mt-2">
          <div className="flex items-center justify-between mb-3">
            <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
              {t.subscriptions.triggerHistory.title}
            </Label>
            <Badge variant="outline" className="text-[10px]">
              {historyLoading ? '...' : triggerHistory.length}
            </Badge>
          </div>
          {historyLoading ? (
            <div className="space-y-3 py-1" role="status">
              <span className="sr-only">{t.common.loading}</span>
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="flex items-center justify-between gap-3">
                  <Skeleton className="h-4 w-28 flex-shrink-0" />
                  <div className="flex items-center gap-2">
                    <Skeleton className="h-5 w-14 rounded-full" />
                    <Skeleton className="h-4 w-4 flex-shrink-0" />
                  </div>
                </div>
              ))}
            </div>
          ) : triggerHistory.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-4">
              {t.subscriptions.triggerHistory.empty}
            </p>
          ) : (
            <>
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>{t.subscriptions.triggerHistory.date}</TableHead>
                  <TableHead className="text-right">{t.subscriptions.triggerHistory.delivery.title}</TableHead>
                  <TableHead className="w-8" aria-label={t.subscriptions.triggerHistory.chart.title} />
                </TableRow>
              </TableHeader>
              <TableBody>
                {triggerHistory.slice(0, historyVisibleCount).map((entry) => (
                  <TableRow
                    key={entry.id}
                    onClick={() => openTriggerPage(entry)}
                    className="cursor-pointer transition-colors hover:bg-muted/60"
                  >
                    <TableCell className="text-xs tabular-nums whitespace-nowrap">
                      {formatTriggerDate(entry.triggeredAt, locale)}
                    </TableCell>
                    <TableCell className="text-right align-middle">
                      <div className="flex justify-end">
                        <TriggerDeliveries deliveries={entry.deliveries} t={t} locale={locale} />
                      </div>
                    </TableCell>
                    <TableCell className="w-8 pr-2 text-right align-middle leading-none">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        onClick={(e) => {
                          // The row is clickable too; without this the bubbling click would navigate twice
                          e.stopPropagation();
                          openTriggerPage(entry);
                        }}
                        aria-label={t.subscriptions.triggerHistory.chart.title}
                      >
                        <ChevronRight className="h-4 w-4 text-muted-foreground" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            {historyVisibleCount < triggerHistory.length && (
              <div ref={historySentinelRef} className="h-4" />
            )}
            {historyVisibleCount >= triggerHistory.length && triggerHistory.length >= TRIGGER_HISTORY_SERVER_LIMIT && (
              <p className="text-xs text-muted-foreground text-center py-2">
                {t.subscriptions.triggerHistory.limitReached.replace('{{count}}', String(triggerHistory.length))}
              </p>
            )}
            </>
          )}
        </div>

        {/* Delete confirmation */}
        <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-destructive flex-shrink-0" />
                {t.subscriptions.delete.confirm}
              </AlertDialogTitle>
              <AlertDialogDescription>
                {t.subscriptions.delete.message.replace('<code>', '').replace('{ticker}', primary?.ticker ?? '').replace('</code>', '')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t.common.cancel}</AlertDialogCancel>
              <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={handleDelete}>
                {t.common.delete}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </div>
  );
}
