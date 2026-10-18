import { QueryClient } from '@tanstack/react-query';
import { classifyApiError } from '@/services/api';

/**
 * Single client for all server reads. Pages previously fetched in mount
 * effects, so every navigation (and the "back" one especially) re-downloaded
 * everything the previous page already had in its discarded local state.
 * staleTime mirrors the hand-rolled 30 s module caches it replaces:
 * navigating back within the window is served from memory without network.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // api.ts already classifies failures (network/api) and the pages render
      // the error states; a silent RQ retry would only delay those states
      retry: 0,
      refetchOnWindowFocus: false,
    },
  },
});

/** Query keys shared across pages so the same entity is fetched once. */
export const qk = {
  config: ['config'] as const,
  siteConfig: ['site-config'] as const,
  providers: ['providers'] as const,
  onboarding: ['onboarding'] as const,
  subscriptions: ['subscriptions'] as const,
  // The list page's query carries the chip filters in its key; the base
  // ['subscriptions'] prefix still invalidates/updates every filtered variant
  subscriptionsList: (q: string, statuses: string, type: string, indicator: string, exchange: string, triggered: string) =>
    ['subscriptions', 'list', q, statuses, type, indicator, exchange, triggered] as const,
  subscription: (id: string) => ['subscription', id] as const,
  triggerHistory: (id: string) => ['trigger-history', id] as const,
  subscriptionSnapshot: (id: string) => ['subscription-snapshot', id] as const,
  snapshotChartData: (id: string, chartId: string) => ['snapshot-chart-data', id, chartId] as const,
  triggerChartData: (id: string, triggerId: string) => ['trigger-chart-data', id, triggerId] as const,
  indicators: ['indicators'] as const,
  indicatorCatalog: (lang: string) => ['indicator-catalog', lang] as const,
  rulePresets: (lang: string) => ['rule-presets', lang] as const,
  landingExamples: (lang: string) => ['landing-examples', lang] as const,
  indicatorTemplate: (name: string, lang: string) => ['indicator-template', name, lang] as const,
  indicatorExample: (name: string, lang: string) => ['indicator-example', name, lang] as const,
  instruments: (page: number, pageSize: number, search: string, type: string) =>
    ['instruments', page, pageSize, search, type] as const,
  instrument: (ticker: string, exchange: string | null | undefined) =>
    ['instrument', ticker, exchange ?? ''] as const,
  instrumentChart: (ticker: string, exchange: string | null | undefined) =>
    ['instrument-chart', ticker, exchange ?? ''] as const,
  telegramChats: ['telegram-chats'] as const,
  pushSubscriptions: ['push-subs'] as const,
  notificationTemplates: ['notification-templates'] as const,
};

/** Maps a query error to the localized message the pages used to pick by hand. */
export function apiErrorText(
  e: unknown,
  t: { errors: { noInternet: string; apiUnavailable: string; unknown: string } },
): string {
  const errType = classifyApiError(e);
  if (errType === 'network') return t.errors.noInternet;
  if (errType === 'api') return t.errors.apiUnavailable;
  return t.errors.unknown;
}
