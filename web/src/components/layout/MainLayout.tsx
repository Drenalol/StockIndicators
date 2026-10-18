import { useEffect, useMemo, useRef, useState, useContext, cloneElement, Suspense } from 'react';
import { NavLink, useNavigate, useLocation, matchPath, useOutlet, UNSAFE_LocationContext } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import { useTranslation } from '@/i18n';
import { RouteFallback } from '@/components/RouteFallback';
import { cn } from '@/lib/utils';
import { useAuth } from '@/hooks/useAuth';
import { useTheme } from '@/hooks/useTheme';
import {
  ChartCandlestick, Bell, Landmark, Settings, Sun, Moon, Monitor,
} from 'lucide-react';
import { useNavigationGuard } from '@/components/layout/NavigationGuardProvider';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useKeyboardOpen } from '@/hooks/useKeyboardOpen';
import OnboardingTour from '@/components/layout/OnboardingTour';
import Footer from '@/components/layout/Footer';
import { Logo } from '@/components/Logo';
import { BoostyIcon } from '@/components/BoostyIcon';
import { dashboardPrefix } from '@/lib/paths';
import { BOOSTY_URL } from '@/lib/support';

export default function MainLayout() {
  const { t, locale } = useTranslation();
  const { isAuthenticated, loaded, profile, completeOnboarding } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const outlet = useOutlet();
  // The router location as seen at this render. AnimatePresence keeps the old
  // motion.div (with the provider element captured here) for the exit animation,
  // so the exiting page resolves useLocation/useSearchParams against this frozen
  // value instead of the new URL - otherwise it would re-render on the target
  // route's query (e.g. ?ticker from /subscriptions/new) and paint a phantom
  // detail view (GET /instrument/{ticker} without exchange) while fading out.
  const locationContext = useContext(UNSAFE_LocationContext);
  const { theme, systemTheme, setTheme } = useTheme();
  const { confirmNavigation } = useNavigationGuard();
  // the hook also maintains --kb-inset / html[data-keyboard] globally (index.css);
  // here it additionally retracts the bottom tab bar while the keyboard is up
  const keyboardOpen = useKeyboardOpen();

  const pathParts = useMemo(
    () => location.pathname.split('/').filter(Boolean),
    [location.pathname]
  );

  // Top-level section (subscriptions/indicators/instruments/settings),
  // sitting under the "/{locale}/dashboard" namespace
  const sectionKey = pathParts[2] ?? '';

  // Inside subscriptions the list view is a single page: bare /subscriptions and
  // /subscriptions/:ticker render the same component (the ticker only marks the
  // expanded group), so expanding/collapsing a group must not transition the page.
  // /new, detail and edit are separate pages.
  // Indicators: bare /indicators is the list, /indicators/:name is the detail page.
  // Settings: bare /settings is the page, deeper paths (settings/billing) are own pages.
  const transitionKey = sectionKey === 'subscriptions'
    ? (pathParts.length <= 4
        ? (pathParts[3] === 'new' ? 'subscriptions/new' : 'subscriptions')
        : pathParts.join('/'))
    : sectionKey === 'indicators' || sectionKey === 'settings'
      ? (pathParts.length > 3 ? pathParts.join('/') : sectionKey)
      : sectionKey;

  // Page transitions are fade-only on purpose: a transform on the animated wrapper
  // turns it into the containing block for any position:fixed child (e.g. the mobile
  // action bar), so the footer would track the moving box and snap when the transform
  // clears. Opacity does not create a containing block, so fixed children stay put.

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [location.pathname]);

  // The URL prefix always tracks the saved locale (App.tsx rewrites any URL
  // whose prefix disagrees with it), so links are built from the context locale
  const prefix = dashboardPrefix(locale);

  const navItems = useMemo(() => [
    { to: `${prefix}/subscriptions`, label: t.nav.subscriptions, icon: Bell, tourKey: 'subscriptions' },
    { to: `${prefix}/indicators`, label: t.nav.indicators, icon: ChartCandlestick, tourKey: 'indicators' },
    { to: `${prefix}/instruments`, label: t.nav.instruments, icon: Landmark, tourKey: 'instruments' },
    { to: `${prefix}/settings`, label: t.nav.settings, icon: Settings, tourKey: 'settings' },
  ], [prefix, t.nav.subscriptions, t.nav.indicators, t.nav.instruments, t.nav.settings]);

  // First-login tour: one step per nav item, anchored via data-tour
  const navTourSteps = useMemo(
    () => (['subscriptions', 'indicators', 'instruments', 'settings'] as const)
      .map(key => ({ anchor: key, ...t.onboarding.steps[key] })),
    [t]
  );

  // Single cycling theme button: system -> opposite of the system scheme ->
  // the system scheme itself -> back to system
  const themeCycle = useMemo(() => ({
    system: { icon: Monitor, label: t.theme.system },
    light: { icon: Sun, label: t.theme.light },
    dark: { icon: Moon, label: t.theme.dark },
  }), [t.theme.light, t.theme.dark, t.theme.system]);
  const { icon: ThemeIcon, label: themeLabel } = themeCycle[theme];
  const cycleTheme = () => {
    if (theme === 'system') setTheme(systemTheme === 'dark' ? 'light' : 'dark');
    else if (theme === systemTheme) setTheme('system');
    else setTheme(systemTheme);
  };

  // Radix Tooltip instead of title=: native tooltips never render in touch
  // WebViews, so a touch press additionally opens the styled bubble briefly
  // (the same trick as TouchHintIconButton in the settings page)
  const [themeHintOpen, setThemeHintOpen] = useState(false);
  const themeHintTimer = useRef<number | null>(null);
  useEffect(() => () => {
    if (themeHintTimer.current) window.clearTimeout(themeHintTimer.current);
  }, []);
  const showThemeHint = () => {
    if (themeHintTimer.current) window.clearTimeout(themeHintTimer.current);
    setThemeHintOpen(true);
    themeHintTimer.current = window.setTimeout(() => {
      themeHintTimer.current = null;
      setThemeHintOpen(false);
    }, 2000);
  };

  useEffect(() => {
    if (loaded && !isAuthenticated) navigate(`/${locale}/login`);
  }, [isAuthenticated, loaded, navigate, locale]);

  const isSubscriptionRoute = (pathname: string): boolean => {
    return !!matchPath(`${prefix}/subscriptions/*`, pathname);
  };

  const isIndicatorRoute = (pathname: string): boolean => {
    return !!matchPath(`${prefix}/indicators/*`, pathname);
  };

  const NavLinkButton = ({ to, label, icon: Icon, tourKey }: { to: string; label: string; icon: typeof Bell; tourKey: string }) => {
    const isActive = to === `${prefix}/subscriptions`
      ? isSubscriptionRoute(location.pathname)
      : to === `${prefix}/indicators`
        ? isIndicatorRoute(location.pathname)
        : location.pathname === to;
    const handleClick = (e: React.MouseEvent) => {
      e.preventDefault();
      if (isActive && location.pathname === to) {
        window.scrollTo({ top: 0, behavior: 'smooth' });
        return;
      }
      confirmNavigation(() => navigate(to));
    };
    return (
      <NavLink
        to={to}
        data-tour={tourKey}
        onClick={handleClick}
        className={cn(
          // mobile: full-height, equally-sized thumb targets (>= 44px) inside the bottom nav
          'flex flex-col md:flex-row items-center justify-center gap-1 md:gap-1.5 w-full md:w-auto flex-1 md:flex-none min-h-11 md:min-h-0 px-2 py-1.5 md:px-3 md:py-2 rounded-md text-xs md:text-sm font-medium transition-colors',
          isActive
            ? 'text-primary'
            : 'text-muted-foreground hover:text-foreground'
        )}
      >
        <Icon className="h-6 w-6 md:h-5 md:w-5 flex-shrink-0" />
        <span className="hidden md:inline">{label}</span>
      </NavLink>
    );
  };

  return (
    // min-h subtracts --kb-inset (hooks/useKeyboardOpen): with the keyboard up the
    // document must not exceed the visible band, otherwise iOS keeps scrolling the
    // layout viewport (the keys reveal) and the page paints scrolled-past/blank
    <div className="min-h-[calc(100dvh_-_var(--kb-inset))] bg-background flex flex-col">
      {/* Chrome is tied to isAuthenticated, not loaded: when /auth/me resolves as
          anonymous, loaded flips true one frame before MainLayout's redirect to
          login, so gating on loaded alone still flashes the signed-in chrome */}
      {/* pt-[var(--safe-t)] keeps the logo and the header action icons below the status bar /
          dynamic island: Capacitor's WKWebView is full-screen (contentInsetAdjustmentBehavior
          = .never), so the header is the only thing insetting the top safe area */}
      {isAuthenticated && (
      <header className="sticky top-0 z-40 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 pt-[var(--safe-t)]">
        <div className="relative w-full px-3 md:px-4">
          <div className="relative mx-auto flex h-20 w-full max-w-3xl items-center gap-2 md:gap-4">
            <Logo className="h-9 md:h-11" />

            {/* desktop nav is centered inside the content column, independent of the logo width */}
            <nav className="hidden md:flex md:absolute md:left-1/2 md:-translate-x-1/2 items-center gap-0.5">
              {navItems.map(({ to, label, icon: Icon, tourKey }) => (
                <NavLinkButton key={to} to={to} label={label} icon={Icon} tourKey={tourKey} />
              ))}
            </nav>
          </div>

          {/* pinned to the right edge of the window (not the centered content column) */}
          <TooltipProvider delayDuration={100}>
            <div className="absolute right-3 md:right-4 top-1/2 -translate-y-1/2 flex items-center gap-1">
              <Tooltip
                open={themeHintOpen}
                onOpenChange={(next) => { if (!next && themeHintTimer.current) return; setThemeHintOpen(next); }}
              >
                <TooltipTrigger asChild>
                  <button
                    aria-label={themeLabel}
                    onClick={cycleTheme}
                    onPointerDown={(e) => { if (e.pointerType === 'touch') showThemeHint(); }}
                    className="flex size-11 flex-shrink-0 items-center justify-center rounded-md text-muted-foreground hover:text-foreground hover:bg-accent"
                  >
                    <ThemeIcon className="h-6 w-6 md:h-5 md:w-5" />
                  </button>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="max-w-56 text-center">{themeLabel}</TooltipContent>
              </Tooltip>
              {/* support link: tapping performs the navigation itself, so on touch the
                  tooltip is just a brief flash and the new tab is the explanation */}
              <Tooltip>
                <TooltipTrigger asChild>
                  <a
                    href={BOOSTY_URL}
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label={t.footer.support}
                    className="flex size-11 flex-shrink-0 items-center justify-center rounded-md text-muted-foreground hover:text-foreground hover:bg-accent"
                  >
                    <BoostyIcon className="h-6 w-6 md:h-5 md:w-5" />
                  </a>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="max-w-56 text-center">{t.footer.support}</TooltipContent>
              </Tooltip>
            </div>
          </TooltipProvider>
        </div>
      </header>
      )}

      <main className="mx-auto w-full max-w-3xl px-3 md:px-4 pt-[var(--main-pt)] pb-[var(--main-pb)] flex-1">
        <AnimatePresence mode="wait" initial={false}>
          <motion.div
            key={transitionKey}
            // fade-only: see note above — a transform here would trap fixed children
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2, ease: 'easeInOut' }}
          >
            <UNSAFE_LocationContext.Provider value={locationContext}>
              {outlet && (
                <Suspense fallback={<RouteFallback />}>
                  {cloneElement(outlet, { key: transitionKey })}
                </Suspense>
              )}
            </UNSAFE_LocationContext.Provider>
          </motion.div>
        </AnimatePresence>
      </main>

      {isAuthenticated && <Footer />}

      {isAuthenticated && !keyboardOpen && (
      // row is h-14 (= --nav-h minus the inset); pb keeps the nav background painting
      // the home-indicator band; hidden entirely while the soft keyboard is up
      // (--nav-h collapses in index.css, the keyboard covers the whole bar anyway)
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 border-t bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 pb-[var(--safe-b)]">
        <div className="flex items-stretch h-14 px-1">
          {navItems.map(({ to, label, icon: Icon, tourKey }) => (
            <NavLinkButton key={to} to={to} label={label} icon={Icon} tourKey={tourKey} />
          ))}
        </div>
      </nav>
      )}

      <OnboardingTour
        open={loaded && isAuthenticated && profile?.onboardingCompleted === false}
        steps={navTourSteps}
        onDone={completeOnboarding}
      />

    </div>
  );
}
