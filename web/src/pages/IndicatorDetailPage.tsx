import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation } from '@/i18n';
import { useGoBackOr } from '@/hooks/useGoBackOr';
import { getIndicatorFullNameWithAbbr, getIndicatorCategoryLabel } from '@/i18n/indicatorTranslations';
import { api } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { dashboardPrefix } from '@/lib/paths';
import { IndicatorDetailView } from '@/components/IndicatorDetailView';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ArrowLeft } from 'lucide-react';

export default function IndicatorDetailPage() {
  const params = useParams();
  const name = params.name!;
  const navigate = useNavigate();
  const { loaded, isAuthenticated } = useAuth();
  const { t, locale } = useTranslation();
  const goBackOr = useGoBackOr();
  const prefix = dashboardPrefix(locale);

  const authed = loaded && isAuthenticated;

  const templateQuery = useQuery({
    queryKey: qk.indicatorTemplate(name, locale),
    queryFn: () => api.getIndicatorTemplate(name, locale),
    enabled: authed,
  });
  const template = templateQuery.data ?? null;
  const loading = templateQuery.isPending;
  const error = templateQuery.error ? apiErrorText(templateQuery.error, t) : null;

  const exampleQuery = useQuery({
    queryKey: qk.indicatorExample(name, locale),
    queryFn: () => api.getIndicatorExample(name, locale),
    enabled: authed,
    retry: false,
  });

  // The page is reachable from the indicators list and from a subscription
  // detail (indicator link), so the arrow pops history; the list is the
  // fallback for a fresh load/deep link
  // Built-in one-tap presets for this indicator (backend seed catalog)
  const presetsQuery = useQuery({
    queryKey: qk.rulePresets(locale),
    queryFn: () => api.getRulePresets(locale),
    enabled: authed,
  });
  const indicatorPresets = useMemo(
    () => (presetsQuery.data ?? []).filter((preset) => preset.indicator === name),
    [presetsQuery.data, name],
  );

  const handleBack = () => goBackOr(`${prefix}/indicators`);
  const handleCreate = () => navigate(`${prefix}/subscriptions/new?indicator=${encodeURIComponent(name)}&from=indicators`);

  const header = (
    <div className="space-y-2">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" onClick={handleBack} aria-label={t.common.back}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <h1 className="text-xl md:text-2xl font-bold min-w-0 truncate">{getIndicatorFullNameWithAbbr(name, t)}</h1>
      </div>
      <Badge variant="secondary">{getIndicatorCategoryLabel(name, t)}</Badge>
    </div>
  );

  // Blank while auth is unresolved or anonymous (MainLayout redirects to login);
  // the fetches above run in parallel, so authenticated loads lose no time
  if (!loaded || !isAuthenticated) return null;

  return (
    <div className="flex flex-col h-[var(--app-page-h)]">
      {/* Fixed viewport-fit height — the page must never scroll the browser window (see AGENTS.md) */}
      {header}

      <div className="relative mt-6 flex-1 min-h-0 -mx-1.5 px-1.5 overflow-y-auto pb-2">
        <IndicatorDetailView
          template={template}
          loading={loading}
          error={error}
          example={{ pending: exampleQuery.isPending, error: exampleQuery.isError, data: exampleQuery.data }}
          presets={indicatorPresets}
          onPickPreset={(preset) => navigate(`${prefix}/subscriptions/new?indicator=${encodeURIComponent(name)}&from=indicators&preset=${encodeURIComponent(preset.key)}`)}
          onCreate={handleCreate}
        />
      </div>
    </div>
  );
}
