import { useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { getIndicatorFullNameWithAbbr } from '@/i18n/indicatorTranslations';
import { api, ApiError } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import type { Translations } from '@/i18n';
import { primaryApiCondition } from '@/components/RuleEditor/schema';
import { InteractiveChart } from '@/components/InteractiveChart';
import { TriggerDeliveries } from '@/components/TriggerDeliveries';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InstrumentLogo } from '@/components/InstrumentLogo';
import { formatTriggerDate } from '@/lib/datetime';
import { dashboardPrefix } from '@/lib/paths';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { ArrowLeft, LineChart } from 'lucide-react';

/**
 * Stable "there is nothing to draw" codes the chart endpoint answers with
 * (TriggerChartResolution.*); an unknown code keeps the generic no-data text.
 */
const CHART_UNAVAILABLE_RE = /^chart_unavailable:([a-z0-9_]+)$/;

type ChartErrorCatalog = Translations['subscriptions']['triggerHistory']['chart']['errors'];

function chartUnavailableText(error: unknown, fallback: string, errors: ChartErrorCatalog): string {
  if (!(error instanceof ApiError)) return fallback;

  const match = CHART_UNAVAILABLE_RE.exec(error.message);
  if (!match) return fallback;

  const key = match[1].replace(/_([a-z0-9])/g, (_, c: string) => c.toUpperCase()) as keyof ChartErrorCatalog;

  return errors[key] ?? fallback;
}

export default function TriggerChartPage() {
  const params = useParams();
  const subId = params.subId!;
  const triggerId = params.triggerId!;
  const { loaded, isAuthenticated } = useAuth();
  const { t, locale } = useTranslation();
  const goBackOr = useGoBackOr();
  const prefix = dashboardPrefix(locale);

  const authed = loaded && isAuthenticated;

  const subscriptionQuery = useQuery({
    queryKey: qk.subscription(subId),
    queryFn: () => api.getSubscription(subId),
    enabled: authed,
  });
  const subscription = subscriptionQuery.data ?? null;
  // The subscription stores no instrument/indicator — the header identity is the
  // first leaf condition of its rule
  const primary = subscription ? primaryApiCondition(subscription.rule) : null;
  const loading = subscriptionQuery.isPending;

  // The metadata (time/value) comes from the trigger history — there is no
  // single-trigger endpoint, so a trigger older than the server limit (100)
  // is reported as not found. The history is the very list the user navigated
  // from, so the shared cache answers the "back" navigation without network.
  const historyQuery = useQuery({
    queryKey: qk.triggerHistory(subId),
    queryFn: () => api.getTriggerHistory(subId),
    enabled: authed,
  });
  const entry = useMemo(
    () => historyQuery.data?.find((h) => h.id === triggerId) ?? null,
    [historyQuery.data, triggerId],
  );
  const entryLoading = historyQuery.isPending;

  // The chart renders from raw series data (v2) on every plan; the crosshair legend,
  // zoom and fullscreen live inside InteractiveChart
  const chartDataQuery = useQuery({
    queryKey: qk.triggerChartData(subId, triggerId),
    queryFn: () => api.getTriggerChartData(subId, triggerId),
    enabled: authed && !!entry,
    // A 404 here is permanent (nothing to draw for this trigger), so it is not worth four
    // identical requests and four identical server warnings; a failed render stays retryable
    retry: (failureCount, error) =>
      error instanceof ApiError && error.statusCode === 404 ? false : failureCount < 3,
  });

  // Usually entered from the subscription detail, so the arrow pops history
  // back there; the subscription/list URL is the fallback for a deep link
  const handleBack = () => {
    const ticker = params.ticker ?? primary?.ticker;
    goBackOr(ticker ? `${prefix}/subscriptions/${ticker}/${subId}` : `${prefix}/subscriptions`);
  };

  // Blank while auth is unresolved or anonymous (MainLayout redirects to login)
  if (!loaded || !isAuthenticated) return null;

  if (loading) {
    return (
      <div className="flex flex-col h-[var(--app-page-h)]" role="status">
        <span className="sr-only">{t.common.loading}</span>
        <div className="space-y-2">
          <div className="flex items-center gap-3">
            <Skeleton className="h-10 w-10 rounded-md flex-shrink-0" />
            <Skeleton className="h-7 w-48" />
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Skeleton className="h-7 w-20" />
            <Skeleton className="h-7 w-5 rounded-full" />
            <Skeleton className="h-7 w-24 rounded-full" />
          </div>
        </div>
        <div className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-4">
          <Skeleton className="h-4 w-44" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
        </div>
      </div>
    );
  }

  if (subscriptionQuery.error || !subscription) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={handleBack} aria-label={t.common.back}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <h1 className="text-xl md:text-2xl font-bold">{t.subscriptions.triggerHistory.chart.title}</h1>
        </div>
        <p className="text-muted-foreground">{subscriptionQuery.error ? apiErrorText(subscriptionQuery.error, t) : t.errors.failedToLoad}</p>
      </div>
    );
  }

  const chartNoData = (
    <div className="flex flex-col items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
      <LineChart className="h-6 w-6" />
      <p className="text-center">
        {chartUnavailableText(
          chartDataQuery.error,
          t.subscriptions.triggerHistory.chart.noData,
          t.subscriptions.triggerHistory.chart.errors,
        )}
      </p>
    </div>
  );

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height — the page must never scroll the browser window (see AGENTS.md) */}
      {/* Header: back to the subscription + chart title */}
      <div className="space-y-2">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={handleBack} aria-label={t.common.back} title={t.common.back}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <h1 className="text-xl md:text-2xl font-bold truncate">
            {t.subscriptions.triggerHistory.chart.title}
          </h1>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <InstrumentLogo ticker={primary?.ticker ?? ''} exchangeName={primary?.exchangeName} size="xs" />
          <code className="bg-muted px-2 py-1 rounded text-sm font-semibold">{primary?.ticker ?? '-'}</code>
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
        </div>
      </div>

      <div className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2 space-y-4">
        {entryLoading ? (
          <div className="space-y-4" role="status">
            <span className="sr-only">{t.common.loading}</span>
            <Skeleton className="h-4 w-44" />
            <Skeleton className="h-4 w-28" />
            <div className="flex flex-wrap items-center gap-2">
              <Skeleton className="h-4 w-16" />
              <Skeleton className="h-5 w-14 rounded-full" />
              <Skeleton className="h-5 w-14 rounded-full" />
            </div>
            <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
          </div>
        ) : !entry ? (
          <p className="text-sm text-muted-foreground text-center py-10">
            {t.subscriptions.triggerHistory.chart.notFound}
          </p>
        ) : (
          <>
            <div className="flex flex-wrap items-center justify-between gap-1 text-xs text-muted-foreground">
              <span>
                {t.subscriptions.triggerHistory.chart.time}: {formatTriggerDate(entry.triggeredAt, locale)}
              </span>
              {/* New rows anchor the chart to the candle they saw; legacy rows anchored
                  TriggeredAt itself, so there is nothing second to show for them */}
              {entry.triggeredCandleAt && entry.triggeredCandleAt !== entry.triggeredAt && (
                <span>
                  {t.subscriptions.triggerHistory.chart.candle}: {formatTriggerDate(entry.triggeredCandleAt, locale)}
                </span>
              )}
            </div>
            <p className="text-xs font-mono break-words">
              <span className="text-muted-foreground">{t.subscriptions.triggerHistory.chart.value}: </span>
              {entry.triggerValue}
            </p>
            {entry.deliveries && entry.deliveries.length > 0 && (
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs text-muted-foreground">{t.subscriptions.triggerHistory.delivery.title}:</span>
                <TriggerDeliveries deliveries={entry.deliveries} t={t} locale={locale} />
              </div>
            )}
            {chartDataQuery.isPending ? (
              <div role="status">
                <span className="sr-only">{t.subscriptions.triggerHistory.chart.loading}</span>
                <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
              </div>
            ) : chartDataQuery.data ? (
              <InteractiveChart data={chartDataQuery.data} />
            ) : (
              chartNoData
            )}
          </>
        )}
      </div>
    </div>
  );
}
