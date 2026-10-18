import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useTranslation, SUPPORTED_LOCALES, type Locale } from '@/i18n';
import { api } from '@/services/api';
import { qk, apiErrorText } from '@/lib/query';
import { PublicPageHeader } from '@/components/layout/PublicPageHeader';
import { IndicatorsCatalog } from '@/components/IndicatorsCatalog';

/**
 * Public indicators catalog at /{locale}/indicators: every indicator with its
 * localized description, browsable without an account. Shares the catalog
 * endpoint and the grid component with the dashboard page, so both lists always
 * show exactly the same (reflection-derived) content. A regular scrolling
 * document like the landing and the legal pages, not an app page.
 */
export default function PublicIndicatorsPage() {
  const { t, locale } = useTranslation();
  const { locale: pathLocale } = useParams();
  const navigate = useNavigate();

  const routeLocale = (SUPPORTED_LOCALES.includes((pathLocale ?? '') as Locale)
    ? pathLocale
    : locale) as Locale;

  const catalogQuery = useQuery({
    queryKey: qk.indicatorCatalog(routeLocale),
    queryFn: () => api.getIndicatorCatalog(routeLocale),
  });
  const items = useMemo(() => catalogQuery.data ?? [], [catalogQuery.data]);
  const error = catalogQuery.error ? apiErrorText(catalogQuery.error, t) : null;

  return (
    <div className="min-h-[100dvh] bg-background text-foreground flex flex-col">
      <PublicPageHeader backFallback={`/${routeLocale}`} wide />

      <main className="mx-auto w-full max-w-5xl px-3 md:px-4 py-8 pb-[calc(2rem+var(--safe-b))]">
        <h1 className="text-2xl font-bold">{t.indicators.title}</h1>
        <p className="mt-1 text-sm text-muted-foreground md:text-base">{t.indicators.publicHint}</p>

        <div className="mt-8">
          <IndicatorsCatalog
            items={items}
            pending={catalogQuery.isPending}
            error={error}
            onOpen={(name) => navigate(`/${routeLocale}/indicators/${encodeURIComponent(name)}`)}
            hrefFor={(name) => `/${routeLocale}/indicators/${encodeURIComponent(name)}`}
          />
        </div>
      </main>
    </div>
  );
}
