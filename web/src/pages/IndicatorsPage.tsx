import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation, type Locale } from '@/i18n';
import { api } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { dashboardPrefix } from '@/lib/paths';
import { IndicatorsCatalog } from '@/components/IndicatorsCatalog';

export default function IndicatorsPage() {
  const params = useParams();
  const { t, locale } = useTranslation();
  const navigate = useNavigate();
  const routeLocale = (params.locale as Locale) || locale;
  const { loaded, isAuthenticated } = useAuth();
  const prefix = dashboardPrefix(routeLocale);

  // The same public catalog endpoint the landing indicators page uses: names,
  // premium flags and localized descriptions come from one reflection source,
  // so a new indicator appears on both surfaces automatically.
  const catalogQuery = useQuery({
    queryKey: qk.indicatorCatalog(routeLocale),
    queryFn: () => api.getIndicatorCatalog(routeLocale),
    enabled: loaded && isAuthenticated,
  });
  const items = useMemo(() => catalogQuery.data ?? [], [catalogQuery.data]);
  const error = catalogQuery.error ? apiErrorText(catalogQuery.error, t) : null;

  // Fixed viewport-fit height — the page must never scroll the browser window (see AGENTS.md)
  if (!loaded || !isAuthenticated) return null;

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      <h1 className="text-xl md:text-2xl font-bold">{t.indicators.title}</h1>

      {/* flex column: the search form stays pinned, the grid body owns the remaining height */}
      <div className="mt-6 flex min-h-0 flex-1 flex-col">
        <IndicatorsCatalog
          items={items}
          pending={catalogQuery.isPending}
          error={error}
          onOpen={(name) => navigate(`${prefix}/indicators/${encodeURIComponent(name)}`)}
          hrefFor={(name) => `${prefix}/indicators/${encodeURIComponent(name)}`}
          bodyClassName="relative -mx-1.5 flex-1 min-h-0 overflow-y-auto px-1.5 pb-2"
        />
      </div>
    </div>
  );
}
