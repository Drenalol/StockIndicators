import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { AlertTriangle, ArrowRight } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { useTranslation } from '@/i18n';
import { api } from '@/services/api';
import { qk } from '@/lib/query';
import { dashboardPrefix } from '@/lib/paths';

/**
 * First-run nudge: a fresh instance has no provider keys yet, so the watchlist
 * stays empty for a reason nobody explains. Until at least one market-data
 * provider is both enabled and configured, the subscriptions page points the
 * operator at Settings → Providers instead of an unexplained empty list.
 */
export function FirstRunNotice() {
  const { t, locale } = useTranslation();

  const providersQuery = useQuery({
    queryKey: qk.providers,
    queryFn: () => api.getProviders(),
  });
  const providers = providersQuery.data;

  // unknown (still loading) or any data provider works - nothing to nag about
  if (!providers || providers.some((p) => p.kind === 'data' && p.enabled && p.configured)) return null;

  return (
    <Alert className="mt-3 border-amber-500/60 bg-amber-500/10">
      <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
      <AlertTitle>{t.subscriptions.firstRun.title}</AlertTitle>
      <AlertDescription className="text-sm">
        {t.subscriptions.firstRun.text}{' '}
        <Link
          to={`${dashboardPrefix(locale)}/settings?tab=providers`}
          className="inline-flex items-center gap-0.5 font-medium underline underline-offset-2"
        >
          {t.subscriptions.firstRun.action}
          <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </AlertDescription>
    </Alert>
  );
}
