import { lazy, Suspense, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useTranslation, SUPPORTED_LOCALES, type Locale } from '@/i18n';
import MainLayout from '@/components/layout/MainLayout';
import { RouteFallback } from '@/components/RouteFallback';
import LoginPage from '@/pages/LoginPage';
import LandingPage from '@/pages/LandingPage';
import NotFoundPage from '@/pages/NotFoundPage';
import ApiDownOverlay from '@/components/ApiDownOverlay';
import { NavigationGuardProvider } from '@/components/layout/NavigationGuardProvider';
import { useApiHealth } from '@/hooks/ApiHealthProvider';
import { useSiteMode } from '@/lib/siteMode';

// Dashboard pages load on demand: the anonymous landing entry stays small on slow
// mobile networks (they also pull in lightweight-charts/forms, none of which the
// landing needs). Vite emits them as async chunks with automatic modulepreload.
const SubscriptionsPage = lazy(() => import('@/pages/SubscriptionsPage'));
const SubscriptionFormPage = lazy(() => import('@/pages/SubscriptionFormPage'));
const IndicatorsPage = lazy(() => import('@/pages/IndicatorsPage'));
const IndicatorDetailPage = lazy(() => import('@/pages/IndicatorDetailPage'));
const SubscriptionDetailPage = lazy(() => import('@/pages/SubscriptionDetailPage'));
const TriggerChartPage = lazy(() => import('@/pages/TriggerChartPage'));
const SubscriptionSnapshotPage = lazy(() => import('@/pages/SubscriptionSnapshotPage'));
const InstrumentsPage = lazy(() => import('@/pages/InstrumentsPage'));
const SettingsPage = lazy(() => import('@/pages/SettingsPage'));
const TemplatesPage = lazy(() => import('@/pages/TemplatesPage'));
const LegalPage = lazy(() => import('@/pages/LegalPage'));
const PublicIndicatorsPage = lazy(() => import('@/pages/PublicIndicatorsPage'));
const PublicIndicatorDetailPage = lazy(() => import('@/pages/PublicIndicatorDetailPage'));

/**
 * Redirects a non-locale-prefixed URL to its localized form, preserving the
 * path and query. The bare "/" lands on the localized landing page; "?from="
 * on that entry targets a dashboard path instead (used by links that may omit
 * both the locale and the dashboard prefix).
 */
function LocaleRedirect() {
  const { locale } = useTranslation();
  const location = useLocation();
  const [params] = useSearchParams();
  const from = params.get('from');

  const target = location.pathname === '/'
    ? (from ? `/dashboard/${from.replace(/^\/+/, '').replace(/^dashboard\/+/, '')}` : '')
    : location.pathname;

  return <Navigate to={`/${locale}${target}${location.search}${location.hash}`} replace />;
}

/**
 * Old app URLs without the locale prefix (push deep links, bookmarks):
 * localized and namespaced under /dashboard in one hop.
 */
function LegacyBareRedirect() {
  const { locale } = useTranslation();
  const location = useLocation();
  return <Navigate to={`/${locale}/dashboard${location.pathname}${location.search}${location.hash}`} replace />;
}

/** Old localized app URLs ("/{locale}/subscriptions/...") → "/{locale}/dashboard/...". */
function LegacyLocalizedRedirect() {
  const params = useParams();
  const location = useLocation();
  const { locale } = useTranslation();
  const pathLocale = params.locale;
  // The legacy ":locale" also captures the first segment of language-neutral deep
  // links ("/dashboard/subscriptions/new" - the bot's mini app buttons): the route
  // ranks above "/dashboard/*", so an unsupported prefix here is not an old URL but
  // a neutral link waiting for the profile language - localize it instead of 404ing.
  // Genuinely foreign prefixes (/bogus/settings) land on the localized 404 after one
  // hop (the added "/{locale}" no longer matches any legacy pattern, so no loop).
  if (!pathLocale || !SUPPORTED_LOCALES.includes(pathLocale as Locale)) {
    return <Navigate to={`/${locale}${location.pathname}${location.search}${location.hash}`} replace />;
  }
  // the old section (subscriptions/indicators/...) is the segment after the locale -
  // the splat only carries what follows the matched literal, so it is re-emitted here
  const rest = location.pathname.slice(`/${pathLocale}`.length);
  return <Navigate to={`/${pathLocale}/dashboard${rest}${location.search}${location.hash}`} replace />;
}

function LandingPageRoute() {
  const { locale: pathLocale } = useParams();
  if (!pathLocale || !SUPPORTED_LOCALES.includes(pathLocale as Locale)) {
    return <NotFoundPage />;
  }
  return <LandingPage />;
}

/**
 * On a public-site instance the app is not here: an old link, a typed /dashboard or the PWA's
 * start URL lands on the localized landing page instead of an empty sign-in form.
 */
function LandingRedirect() {
  const { locale } = useTranslation();
  return <Navigate to={`/${locale}`} replace />;
}

/**
 * The authenticated part of the SPA, or its absence. The mode comes from the backend, so the
 * same build serves both shapes; while it is unknown the area waits a request rather than
 * flashing a login that a public instance does not have.
 */
function AppArea() {
  const { isPending, data: siteConfig } = useSiteMode();

  if (isPending) return <RouteFallback fullPage />;
  return siteConfig?.landingOnly ? <LandingRedirect /> : <MainLayout />;
}

function DashboardIndexRedirect() {
  const { locale: pathLocale } = useParams();
  const location = useLocation();
  if (!pathLocale || !SUPPORTED_LOCALES.includes(pathLocale as Locale)) {
    return <NotFoundPage />;
  }
  return <Navigate to={`/${pathLocale}/dashboard/subscriptions${location.search}${location.hash}`} replace />;
}

function AppRoutes() {
  const { locale } = useTranslation();
  const location = useLocation();
  const navigate = useNavigate();

  // The user's saved locale is the single source of truth: it is set only by
  // the profile language switch and persists across reloads. A URL carrying a
  // different locale prefix is stale history (entries created before a
  // language switch) or a foreign deep link, so it is rewritten to the current
  // locale instead of switching the language back — browser back then shows
  // the previous page in the user's language, not the old one.
  useEffect(() => {
    const parts = location.pathname.split('/').filter(Boolean);
    const pathLocale = parts[0];
    if (pathLocale && SUPPORTED_LOCALES.includes(pathLocale as Locale) && pathLocale !== locale) {
      navigate(`/${locale}/${parts.slice(1).join('/')}${location.search}${location.hash}`, { replace: true });
    }
  }, [location.pathname, location.search, locale, navigate]);

  return (
    // lazy top-level pages (legal) get a full-page spinner; dashboard pages inside
    // MainLayout's outlet are covered by the layout's own Suspense (header stays)
    <Suspense fallback={<RouteFallback fullPage />}>
    <Routes>
      <Route path="/" element={<LocaleRedirect />} />

      <Route path="/login" element={<AppArea />}>
        <Route index element={<LoginPage />} />
      </Route>
      <Route path="/:locale/login" element={<AppArea />}>
        <Route index element={<LoginPage />} />
      </Route>

      {/* Landing lives at the root: the localized form is a real page,
          the bare "/" resolves to it through LocaleRedirect */}
      <Route path="/:locale" element={<LandingPageRoute />} />

      {/* Non-locale dashboard deep links (push, bookmarks) → localized form */}
      <Route path="/dashboard" element={<LocaleRedirect />} />
      <Route path="/dashboard/*" element={<LocaleRedirect />} />

      {/* Pre-dashboard app URLs (old pushes, Telegram mini app links, bookmarks)
          → localized dashboard form */}
      <Route path="/subscriptions" element={<LegacyBareRedirect />} />
      <Route path="/subscriptions/*" element={<LegacyBareRedirect />} />
      <Route path="/instruments" element={<LegacyBareRedirect />} />
      <Route path="/instruments/*" element={<LegacyBareRedirect />} />
      <Route path="/settings" element={<LegacyBareRedirect />} />
      <Route path="/:locale/subscriptions" element={<LegacyLocalizedRedirect />} />
      <Route path="/:locale/subscriptions/*" element={<LegacyLocalizedRedirect />} />
      <Route path="/:locale/instruments" element={<LegacyLocalizedRedirect />} />
      <Route path="/:locale/instruments/*" element={<LegacyLocalizedRedirect />} />
      <Route path="/:locale/settings" element={<LegacyLocalizedRedirect />} />

      {/* Legal is public (linked from the landing) and must stay reachable
          without the authenticated dashboard chrome */}
      <Route path="/:locale/legal/:slug" element={<LegalPage />} />

      {/* Public indicators catalog (same reflection-derived data and view
          components as the dashboard pages, no user data involved); the old
          /{locale}/indicators legacy redirect was retired in its favour */}
      <Route path="/:locale/indicators" element={<PublicIndicatorsPage />} />
      <Route path="/:locale/indicators/:name" element={<PublicIndicatorDetailPage />} />

      <Route element={<AppArea />}>
        <Route path="/:locale/dashboard" element={<DashboardIndexRedirect />} />
        <Route path="/:locale/dashboard/subscriptions" element={<SubscriptionsPage />} />
        <Route path="/:locale/dashboard/subscriptions/new" element={<SubscriptionFormPage />} />
        <Route path="/:locale/dashboard/subscriptions/:ticker/:subId/edit" element={<SubscriptionFormPage />} />
        <Route path="/:locale/dashboard/subscriptions/:ticker" element={<SubscriptionsPage />} />
        <Route path="/:locale/dashboard/subscriptions/:ticker/:subId" element={<SubscriptionDetailPage />} />
        <Route path="/:locale/dashboard/subscriptions/:ticker/:subId/triggers/:triggerId" element={<TriggerChartPage />} />
        <Route path="/:locale/dashboard/subscriptions/:ticker/:subId/chart" element={<SubscriptionSnapshotPage />} />
        <Route path="/:locale/dashboard/indicators" element={<IndicatorsPage />} />
        <Route path="/:locale/dashboard/indicators/:name" element={<IndicatorDetailPage />} />
        <Route path="/:locale/dashboard/instruments" element={<InstrumentsPage />} />
        <Route path="/:locale/dashboard/settings" element={<SettingsPage />} />
        <Route path="/:locale/dashboard/templates" element={<TemplatesPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
    </Suspense>
  );
}

function AppShell() {
  const { status, offline } = useApiHealth();

  return (
    <>
      <BrowserRouter>
        <NavigationGuardProvider>
          <AppRoutes />
        </NavigationGuardProvider>
      </BrowserRouter>
      {status === 'down' && <ApiDownOverlay offline={offline} />}
    </>
  );
}

// ApiHealthProvider is mounted in main.tsx above AuthProvider (the auth state needs
// the health context to retry the session probe after an outage).
export default function App() {
  return <AppShell />;
}
