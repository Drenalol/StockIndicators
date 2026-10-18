import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useTranslation, SUPPORTED_LOCALES, type Locale } from '@/i18n';
import { getIndicatorFullNameWithAbbr, getIndicatorCategoryLabel } from '@/i18n/indicatorTranslations';
import { api } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { dashboardPrefix } from '@/lib/paths';
import { PublicPageHeader } from '@/components/layout/PublicPageHeader';
import { IndicatorDetailView } from '@/components/IndicatorDetailView';
import { Badge } from '@/components/ui/badge';
import type { RulePresetDto } from '@/types/contracts';

/**
 * Public indicator documentation at /{locale}/indicators/{name}: description,
 * example chart, parameters and one-tap presets, readable without an account.
 * The same endpoints and view component as the authenticated detail page; the
 * create/preset CTAs send an anonymous visitor to /login with the intended
 * subscription form as the post-login target (?from=).
 */
export default function PublicIndicatorDetailPage() {
  const params = useParams();
  const name = params.name!;
  const navigate = useNavigate();
  const { t, locale } = useTranslation();
  const { isAuthenticated } = useAuth();

  const routeLocale = (SUPPORTED_LOCALES.includes((params.locale ?? '') as Locale)
    ? params.locale
    : locale) as Locale;

  const templateQuery = useQuery({
    queryKey: qk.indicatorTemplate(name, routeLocale),
    queryFn: () => api.getIndicatorTemplate(name, routeLocale),
  });
  const template = templateQuery.data ?? null;
  const error = templateQuery.error ? apiErrorText(templateQuery.error, t) : null;

  const exampleQuery = useQuery({
    queryKey: qk.indicatorExample(name, routeLocale),
    queryFn: () => api.getIndicatorExample(name, routeLocale),
    retry: false,
  });

  const presetsQuery = useQuery({
    queryKey: qk.rulePresets(routeLocale),
    queryFn: () => api.getRulePresets(routeLocale),
  });
  const indicatorPresets = useMemo(
    () => (presetsQuery.data ?? []).filter((preset) => preset.indicator === name),
    [presetsQuery.data, name],
  );

  // Anonymous visitors are offered the very form a signed-in user lands in,
  // remembered through the login round trip (see lib/loginFrom).
  const goToForm = (preset?: RulePresetDto) => {
    let query = `indicator=${encodeURIComponent(name)}&from=indicators`;
    if (preset) query += `&preset=${encodeURIComponent(preset.key)}`;

    if (isAuthenticated) {
      navigate(`${dashboardPrefix(routeLocale)}/subscriptions/new?${query}`);
    } else {
      navigate(`/${routeLocale}/login?from=${encodeURIComponent(`dashboard/subscriptions/new?${query}`)}`);
    }
  };

  return (
    <div className="min-h-[100dvh] bg-background text-foreground flex flex-col">
      <PublicPageHeader backFallback={`/${routeLocale}/indicators`} />

      <main className="mx-auto w-full max-w-3xl px-3 md:px-4 py-8 pb-[calc(2rem+var(--safe-b))]">
        <h1 className="text-2xl font-bold min-w-0 break-words">{getIndicatorFullNameWithAbbr(name, t)}</h1>
        <div className="mt-2">
          <Badge variant="secondary">{getIndicatorCategoryLabel(name, t)}</Badge>
        </div>

        <div className="mt-6">
          <IndicatorDetailView
            template={template}
            loading={templateQuery.isPending}
            error={error}
            example={{ pending: exampleQuery.isPending, error: exampleQuery.isError, data: exampleQuery.data }}
            presets={indicatorPresets}
            onPickPreset={(preset) => goToForm(preset)}
            onCreate={() => goToForm()}
          />
        </div>
      </main>
    </div>
  );
}
