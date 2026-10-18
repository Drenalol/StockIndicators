import { useCallback, useMemo, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { useTickerLiveUpdates } from '@/hooks/useTickerLiveUpdates';
import { api, ApiError } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { getTimeFrameLabel } from '@/i18n/indicatorTranslations';
import { InteractiveChart } from '@/components/InteractiveChart';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InstrumentLogo } from '@/components/InstrumentLogo';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { ChartExtendedLine } from '@/components/ChartExtendedLine';
import { ArrowLeft } from 'lucide-react';
import type { SnapshotChartDto } from '@/types/contracts';
import { dashboardPrefix } from '@/lib/paths';

export default function SubscriptionSnapshotPage() {
  const params = useParams();
  const subId = params.subId!;
  const { loaded, isAuthenticated } = useAuth();
  const { t, locale } = useTranslation();
  const goBackOr = useGoBackOr();
  const queryClient = useQueryClient();

  const authed = loaded && isAuthenticated;

  const snapshotQuery = useQuery({
    queryKey: qk.subscriptionSnapshot(subId),
    queryFn: () => api.getSubscriptionSnapshot(subId),
    enabled: authed,
  });
  const snapshot = snapshotQuery.data ?? null;

  // Live updates while this page is open: the hub nudges us when an instrument's
  // candles changed, and the affected charts refetch from the live candle cache
  // (the chart series are always computed server-side from the freshest candles)
  const chartsRef = useRef<SnapshotChartDto[]>([]);
  chartsRef.current = snapshot?.charts ?? [];

  const chartTickers = useMemo(
    () => {
      const seen = new Set<string>();
      const list: { ticker: string; exchange: string | null }[] = [];
      for (const chart of snapshot?.charts ?? []) {
        const key = `${chart.ticker}|${chart.exchangeName ?? ''}`;
        if (seen.has(key)) continue;
        seen.add(key);
        list.push({ ticker: chart.ticker, exchange: chart.exchangeName });
      }
      return list;
    },
    [snapshot],
  );

  const handleLiveTick = useCallback((tick: { ticker: string; exchange: string | null }) => {
    for (const chart of chartsRef.current) {
      const sameTicker = chart.ticker.toLowerCase() === tick.ticker.toLowerCase();
      const sameExchange = (chart.exchangeName ?? '') === (tick.exchange ?? '');
      if (sameTicker && sameExchange) {
        queryClient.invalidateQueries({ queryKey: qk.snapshotChartData(subId, chart.id), refetchType: 'active' });
      }
    }
  }, [queryClient, subId]);

  useTickerLiveUpdates(chartTickers, handleLiveTick, authed && !!snapshot);

  // Blank while auth is unresolved or anonymous (MainLayout redirects to login)
  if (!loaded || !isAuthenticated) return null;

  // Usually entered from the subscription detail, so the arrow pops history
  // back there; on a fresh load (nothing to pop) a bare navigate(-1) would
  // leave the site, so fall back to the subscription detail instead
  const handleBack = () => {
    const ticker = params.ticker;
    goBackOr(ticker ? `${dashboardPrefix(locale)}/subscriptions/${ticker}/${subId}` : `${dashboardPrefix(locale)}/subscriptions`);
  };

  if (snapshotQuery.isPending) {
    return (
      <div className="flex flex-col h-[var(--app-page-h)]" role="status">
        <span className="sr-only">{t.common.loading}</span>
        <div className="flex items-center gap-3 shrink-0">
          <Skeleton className="h-10 w-10 rounded-md flex-shrink-0" />
          <Skeleton className="h-7 w-40" />
        </div>
        <div className="relative mt-4 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2">
          <div className="flex flex-col gap-4">
            {Array.from({ length: 2 }).map((_, i) => (
              <div key={i} className="flex flex-col gap-2">
                <div className="flex flex-wrap items-center gap-2">
                  <Skeleton className="h-7 w-20" />
                  <Skeleton className="h-7 w-5 rounded-full" />
                  <Skeleton className="h-7 w-16 rounded-full" />
                </div>
                <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (snapshotQuery.error || !snapshot) {
    // The backend answers 404 when no chart could be rendered for any instrument (the candle
    // cache window expired) - that is the localized no-data state, not an outage
    const isNoData = snapshotQuery.error instanceof ApiError && snapshotQuery.error.statusCode === 404;

    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={handleBack} aria-label={t.common.back}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <h1 className="text-xl md:text-2xl font-bold">{t.subscriptions.snapshot.title}</h1>
        </div>
        <p className="text-muted-foreground">
          {snapshotQuery.error && !isNoData
            ? apiErrorText(snapshotQuery.error, t)
            : t.subscriptions.snapshot.noData}
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height - the page must never scroll the browser window (see AGENTS.md) */}
      {/* Header: back + title. There is no refresh button: every chart request recomputes
          the series from the live candle cache, and the SignalR push refetches them while
          the page stays open */}
      <div className="flex items-center gap-3 shrink-0">
        <Button variant="ghost" size="icon" onClick={handleBack} aria-label={t.common.back} title={t.common.back}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <h1 className="text-xl md:text-2xl font-bold truncate">{t.subscriptions.snapshot.title}</h1>
      </div>

      {/* Charts scroll inside the page */}
      <div className="relative mt-4 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2">
        {snapshot.charts.length === 0 ? (
          <p className="text-sm text-muted-foreground text-center py-10">{t.subscriptions.snapshot.noData}</p>
        ) : (
          <div className="flex flex-col gap-4">
            {snapshot.charts.map((chart) => (
              <SnapshotChartBlock key={chart.id} subId={subId} chart={chart} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// One stored chart row of the snapshot with its header chrome and interactive chart:
// the row only identifies the instrument/timeframe group, the series (and with them
// the freshness anchor) come from the live candle cache on every request
function SnapshotChartBlock({ subId, chart }: { subId: string; chart: SnapshotChartDto }) {
  const { t } = useTranslation();

  const chartQuery = useQuery({
    queryKey: qk.snapshotChartData(subId, chart.id),
    queryFn: () => api.getSnapshotChartData(subId, chart.id),
  });

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <InstrumentLogo ticker={chart.ticker} exchangeName={chart.exchangeName} size="xs" />
        <code className="bg-muted px-2 py-1 rounded text-sm font-semibold">{chart.ticker}</code>
        {chart.exchangeName && (
          <ExchangeLogo name={chart.exchangeName} />
        )}
        <Badge variant="secondary" className="py-1.5">{getTimeFrameLabel(chart.timeFrame, t)}</Badge>
        {chart.indicators.map((indicator) => (
          <Badge key={indicator} variant="outline" className="py-1.5">{indicator}</Badge>
        ))}
      </div>

      {/* Daily charts merge the extended minutes into the live forming bar server-side;
          this line repeats its price with the session name, like the quote services */}
      <ChartExtendedLine extended={chartQuery.data?.extended} />

      {chartQuery.isPending ? (
        <div role="status">
          <span className="sr-only">{t.subscriptions.snapshot.loading}</span>
          <Skeleton className="aspect-[2/1] w-full rounded-md border border-border" />
        </div>
      ) : !chartQuery.data ? (
        <p className="text-sm text-muted-foreground text-center py-10">
          {t.subscriptions.snapshot.noData}
        </p>
      ) : (
        <InteractiveChart data={chartQuery.data} />
      )}
    </div>
  );
}
