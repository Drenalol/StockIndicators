import { Suspense, lazy, useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { motion, useReducedMotion } from 'motion/react';
import { Bell, Camera, Check, Code2, GitMerge, Landmark, ListTree, Moon, Send, ShieldCheck, Sun, Terminal, Zap } from 'lucide-react';
import { useTranslation, SUPPORTED_LOCALES, LOCALE_NAMES, type Locale } from '@/i18n';
import { useAuth } from '@/hooks/useAuth';
import { useTheme } from '@/hooks/useTheme';
import { api } from '@/services/api';
import { qk } from '@/lib/query';
import { useSiteMode } from '@/lib/siteMode';
import { cn } from '@/lib/utils';
import { BOOSTY_URL } from '@/lib/support';
import { BoostyIcon } from '@/components/BoostyIcon';
import { GitHubIcon } from '@/components/GitHubIcon';
import { Logo } from '@/components/Logo';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { SectionToc, type SectionTocItem } from '@/components/SectionToc';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
} from '@/components/ui/dropdown-menu';

// charts (lightweight-charts + dialog chrome) stay out of the landing entry: the
// below-the-fold gallery pulls the chunk in while its data loads, the skeleton below
// holds the section's height so the swap never shifts the page
const TriggerGallery = lazy(() => import('@/components/TriggerGallery').then((m) => ({ default: m.TriggerGallery })));

function useSeo() {
  const { t } = useTranslation();
  useEffect(() => {
    document.title = t.landing.seo.title;
    let meta = document.head.querySelector<HTMLMetaElement>('meta[name="description"]');
    if (!meta) {
      meta = document.createElement('meta');
      meta.name = 'description';
      document.head.appendChild(meta);
    }
    meta.content = t.landing.seo.description;
  }, [t.landing.seo.title, t.landing.seo.description]);
}

const FEATURE_ICONS = [Zap, Send, Camera, Landmark, ListTree];

/**
 * Same-geometry placeholder for the gallery while its payload loads: identical card,
 * header row, chart aspect and controls row, so swapping in the real gallery (or the
 * section vanishing on error below the fold) never shifts the sections underneath.
 */
function GallerySkeleton() {
  return (
    <div aria-hidden="true">
      <div className="flex pb-1">
        <div className="w-[92%] flex-shrink-0 md:w-[70%]">
          <Card>
            <CardContent className="flex flex-col gap-3 p-4">
              <div className="flex h-11 items-center gap-2">
                <div className="h-6 w-28 rounded-md bg-muted" />
                <div className="ml-auto size-11 rounded-md bg-muted" />
              </div>
              <div className="aspect-[3/4] w-full rounded-md border border-border bg-muted/20 md:aspect-[4/3]" />
            </CardContent>
          </Card>
        </div>
      </div>
      <div className="mt-4 flex items-center justify-center gap-1">
        <div className="size-11 rounded-md bg-muted" />
        <div className="h-1.5 w-16 rounded-full bg-muted" />
        <div className="size-11 rounded-md bg-muted" />
      </div>
    </div>
  );
}

/**
 * Decorative mini rule-tree for the multi-level feature card: the same visual
 * language as the read-only RuleGroupView (an operator badge per group, plain
 * bordered condition chips, a nested group in an accent-bordered box), so the
 * card shows the nesting itself rather than another screenshot.
 */
function RuleTreePreview({ preview, andLabel, orLabel, groupLabel }: {
  preview: { condition1: string; condition2: string; condition3: string };
  andLabel: string;
  orLabel: string;
  groupLabel: string;
}) {
  return (
    <div aria-hidden="true" className="w-full max-w-[260px] flex-shrink-0 self-center rounded-lg border bg-card p-3 sm:self-auto">
      <Badge variant="outline" className="font-semibold uppercase">{orLabel}</Badge>
      <div className="mt-2 space-y-2">
        <div className="rounded-md border bg-background px-3 py-2 text-xs font-medium">{preview.condition3}</div>
        <div className="rounded-lg border border-l-4 border-l-primary/40 bg-muted/30 p-3">
          <div className="flex items-center gap-1.5">
            <GitMerge className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">{groupLabel}</span>
          </div>
          <div className="mt-2 flex items-center gap-2">
            <Badge variant="secondary" className="font-semibold uppercase">{andLabel}</Badge>
          </div>
          <div className="mt-2 space-y-2">
            <div className="rounded-md border bg-background px-3 py-2 text-xs font-medium">{preview.condition1}</div>
            <div className="rounded-md border bg-background px-3 py-2 text-xs font-medium">{preview.condition2}</div>
          </div>
        </div>
      </div>
    </div>
  );
}

/**
 * Public marketing landing at the localized root ("/ru", "/en", "/es"): the
 * open-source pitch - what the engine does, how to install it, where the code
 * lives. Deliberately a regular scrolling document page: the "no browser
 * scroll" app-layout rule covers the dashboard pages inside MainLayout, not
 * this one.
 */
const GITHUB_URL = 'https://github.com/Drenalol/StockIndicators';

export default function LandingPage() {
  const { t, locale, setLocale } = useTranslation();
  const params = useParams();
  // The header button uses the hint-backed flag instead of isAuthenticated, so a
  // returning signed-in visitor does not watch "Sign in" flash before the label
  // flips once GET /auth/me answers (the session is an httpOnly cookie and is
  // unknowable synchronously).
  const { looksAuthenticated } = useAuth();
  const { resolvedTheme, setTheme } = useTheme();
  const reduceMotion = useReducedMotion();
  useSeo();

  // Public anonymous gallery payload (example trigger charts from the candle fixtures).
  // The section below hides when nothing loads.
  const { data: examples, isPending } = useQuery({
    queryKey: qk.landingExamples(locale),
    queryFn: () => api.getLandingExamples(locale),
  });
  const hasGallery = !!examples && examples.length > 0;
  // A public-site instance has no sign-in behind this page: the entry point becomes the
  // installation instead. While the mode is unknown no sign-in affordance renders anywhere on
  // the page - the header slot stays empty, the hero points at the install section - rather
  // than flashing a "Sign in" that a public instance then removes (the probe is same-origin
  // and answers in milliseconds). An unreadable answer means the whole product, as in AppArea.
  const { isPending: modePending, data: siteConfig } = useSiteMode();
  const landingOnly = !!siteConfig?.landingOnly;
  // warm the lazy chart chunk in parallel with the examples fetch, so the gallery
  // does not serialize a second big download after the data arrives
  useEffect(() => {
    import('@/components/TriggerGallery');
  }, []);
  const routeLocale = (SUPPORTED_LOCALES.includes((params.locale ?? '') as Locale)
    ? params.locale
    : locale) as Locale;

  const l = t.landing;

  // Section navigation ticks (desktop): the gallery entry only exists when the section renders
  const tocItems: SectionTocItem[] = [
    { id: 'hero', label: l.toc.hero },
    ...(hasGallery ? [{ id: 'gallery', label: l.toc.gallery }] : []),
    { id: 'features', label: l.toc.features },
    { id: 'steps', label: l.toc.steps },
    { id: 'install', label: l.toc.install },
    { id: 'cta', label: l.toc.cta },
  ];

  return (
    <div className="min-h-[100dvh] bg-background text-foreground flex flex-col">
      <SectionToc items={tocItems} label={l.toc.label} />
      <header className="sticky top-0 z-40 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 pt-[var(--safe-t)]">
        {/* h-20 + 1px border = the 81px app header height (--header-h in index.css), so the
            landing and the dashboard chrome do not jump between each other */}
        <div className="mx-auto flex h-20 w-full max-w-5xl items-center gap-3 px-4">
          <Logo className="h-9 md:h-11" />
          <div className="ml-auto flex items-center gap-1.5">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button
                  aria-label={`${l.nav.language}: ${routeLocale.toUpperCase()}`}
                  className="flex h-11 min-w-11 items-center justify-center rounded-md px-2 text-sm font-medium text-muted-foreground hover:bg-accent hover:text-foreground"
                >
                  {routeLocale.toUpperCase()}
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>{l.nav.language}</DropdownMenuLabel>
                {(SUPPORTED_LOCALES as readonly Locale[]).map((code) => (
                  <DropdownMenuItem key={code} onClick={() => setLocale(code)}>
                    {LOCALE_NAMES[code]}
                    {routeLocale === code && <Check className="ml-auto h-4 w-4" />}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
            <button
              aria-label={t.theme.label}
              onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
              className="flex size-11 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground"
            >
              {resolvedTheme === 'dark' ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
            </button>
            {modePending ? null : landingOnly ? (
              // the install section is the whole point of a public landing: three commands
              <Button asChild className="ml-1 h-11 md:h-10">
                <a href="#install">{l.nav.install}</a>
              </Button>
            ) : looksAuthenticated ? (
              <Button asChild className="ml-1 h-11 md:h-10">
                {/* the full label overflows the 44px-target header row on a phone, so mobile
                    gets the short variant (the row min-content must stay under the px-4 inset) */}
                <Link to={`/${routeLocale}/dashboard`}>
                  <span className="sm:hidden">{l.nav.openAppShort}</span>
                  <span className="hidden sm:inline">{l.nav.openApp}</span>
                </Link>
              </Button>
            ) : (
              <Button asChild className="ml-1 h-11 md:h-10">
                <Link to={`/${routeLocale}/login`}>{l.nav.signIn}</Link>
              </Button>
            )}
          </div>
        </div>
      </header>

      <main className="flex-1">
        {/* Hero; scroll-mt keeps the section title below the sticky header when scrolled to via the toc */}
        {/* grid-cols-1 (not the implicit auto column): an auto column takes the min-content
            width of the fanned alert cards and overflows the phone viewport */}
        <section id="hero" className="mx-auto grid w-full max-w-5xl scroll-mt-24 grid-cols-1 items-center gap-10 px-4 py-14 md:grid-cols-2 md:py-20">
          <motion.div
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.35, ease: 'easeOut' }}
            className="flex flex-col items-center gap-5 md:items-start"
          >
            {/* badges centered on mobile; the wrapped row stretches to the full column, so
                the centering must be reset on desktop (the column is items-start there) */}
            <div className="flex flex-wrap items-center justify-center gap-2 md:justify-start">
              {/* h-auto/min-h instead of a fixed h-7: on a phone the long tag wraps to two
                  lines, and a fixed height squeezes the text against the pill borders */}
              <Badge variant="secondary" className="h-auto min-h-7 px-3 py-1 text-xs">{l.hero.tag}</Badge>
              <Badge variant="outline" className="h-auto min-h-7 gap-1.5 px-3 py-1 text-xs">
                <ShieldCheck className="h-3.5 w-3.5" />
                {l.hero.openSource}
              </Badge>
            </div>
            <h1 className="text-2xl font-bold leading-tight md:text-5xl">{l.hero.title}</h1>
            <p className="text-sm text-muted-foreground md:text-lg">{l.hero.subtitle}</p>
            {/* mobile: the CTA sits at the right edge (thumb reach); desktop keeps the column start */}
            <Button asChild size="lg" className="h-11 self-end px-6 md:self-auto">
              {/* on a public landing "Get started" means installing it, not signing in;
                  while the mode is unknown it installs too - never a login that may not exist */}
              {landingOnly || modePending
                ? <a href="#install">{l.hero.cta}</a>
                : <Link to={`/${routeLocale}/login`}>{l.hero.cta}</Link>}
            </Button>

            <div className="flex w-full flex-wrap items-center justify-center gap-x-8 gap-y-2 pt-2 text-foreground/80 md:justify-start md:gap-x-5">
              {['MOEX', 'NASDAQ', 'NYSE', 'BINANCE'].map((ex) => (
                <ExchangeLogo key={ex} name={ex} size="lg" />
              ))}
            </div>
          </motion.div>

          {/* The inbox of a watchlist: trigger alerts as they would actually arrive.
              Oldest on top, newest at the bottom (a real inbox); the list slides in
              top-to-bottom so the freshest alert appears last. Cards are level; hover lifts one. */}
          <div className="space-y-2">
            {l.hero.alerts.map((alert, i) => (
              <motion.div
                key={alert.indicator}
                initial={{ opacity: 0, x: reduceMotion ? 0 : 28 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ duration: 0.35, ease: 'easeOut', delay: 0.2 + i * 0.12 }}
              >
                <Card className="shadow-xl transition-all duration-200 hover:-translate-y-1 hover:shadow-2xl">
                  <CardContent className="flex items-start gap-3 p-4">
                    <span className="mt-0.5 flex size-9 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
                      <Bell className="h-5 w-5" />
                    </span>
                    <div className="min-w-0">
                      <p className="flex items-center gap-1.5 text-sm font-semibold">
                        <span className="font-mono">{alert.ticker}</span>
                        <ExchangeLogo name={alert.exchange} />
                        <span className="truncate">- {alert.title}</span>
                      </p>
                      {/* two lines on both breakpoints: the md hero column is only
                          half the width, so a single line still cut the body short */}
                      <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">{alert.body}</p>
                    </div>
                    <div className="ml-auto flex flex-shrink-0 flex-col items-end gap-1">
                      <Badge variant="success">{l.hero.statusTriggered}</Badge>
                      <span className="whitespace-nowrap text-[11px] text-muted-foreground">{alert.time}</span>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            ))}
          </div>
        </section>

        {/* Gallery of real example trigger charts (public anonymous endpoint); hidden when it
            fails to load - better an empty spot than a broken mock promise. While loading a
            same-geometry skeleton holds the spot so the sections below never jump (CLS). */}
        {examples && examples.length > 0 && (
          <section id="gallery" className="scroll-mt-24 border-t">
            <div className="mx-auto w-full max-w-5xl px-4 py-14 md:py-20">
              <h2 className="text-xl font-bold md:text-3xl">{l.gallery.title}</h2>
              <p className="mt-3 max-w-2xl text-sm text-muted-foreground md:text-base">{l.gallery.subtitle}</p>
              <div className="mt-8">
                <Suspense fallback={<GallerySkeleton />}>
                  <TriggerGallery examples={examples} />
                </Suspense>
              </div>
              {/* the full public catalog: same indicators, descriptions and example charts, no account */}
              <div className="mt-8 flex justify-center">
                <Button asChild variant="outline" className="h-11 px-6">
                  <Link to={`/${routeLocale}/indicators`}>{l.gallery.browseAll}</Link>
                </Button>
              </div>
            </div>
          </section>
        )}
        {!examples && isPending && (
          <section className="border-t">
            <div className="mx-auto w-full max-w-5xl px-4 py-14 md:py-20">
              <h2 className="text-xl font-bold md:text-3xl">{l.gallery.title}</h2>
              <p className="mt-3 max-w-2xl text-sm text-muted-foreground md:text-base">{l.gallery.subtitle}</p>
              <div className="mt-8">
                <GallerySkeleton />
              </div>
            </div>
          </section>
        )}

        {/* Features */}
        <section id="features" className="scroll-mt-24 border-t bg-muted/40">
          <div className="mx-auto w-full max-w-5xl px-4 py-14 md:py-20">
            <h2 className="text-xl font-bold md:text-3xl">{l.features.title}</h2>
            <div className="mt-8 grid gap-4 sm:grid-cols-2">
              {l.features.items.map((item, i) => {
                const Icon = FEATURE_ICONS[i % FEATURE_ICONS.length];
                // the multi-level card spans the full width and shows the mini rule tree
                const wide = i === l.features.items.length - 1;
                return (
                  <Card key={item.title} className={cn('bg-card', wide && 'sm:col-span-2')}>
                    <CardContent className={cn('gap-4 p-5', wide ? 'flex flex-col sm:flex-row sm:items-center' : 'flex')}>
                      <div className="flex gap-4">
                        <span className="flex size-11 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
                          <Icon className="h-5 w-5" />
                        </span>
                        <div>
                          <h3 className="text-sm font-semibold md:text-base">{item.title}</h3>
                          <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{item.text}</p>
                        </div>
                      </div>
                      {wide && <RuleTreePreview preview={l.features.rulePreview} andLabel={t.rules.and} orLabel={t.rules.or} groupLabel={t.rules.groupLabel} />}
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          </div>
        </section>

        {/* Steps */}
        <section id="steps" className="scroll-mt-24 border-t">
          <div className="mx-auto w-full max-w-5xl px-4 py-14 md:py-20">
            <h2 className="text-xl font-bold md:text-3xl">{l.steps.title}</h2>
            <ol className="mt-8 grid gap-4 md:grid-cols-3">
              {l.steps.items.map((item, i) => (
                <li key={item.title} className="flex flex-col gap-2 rounded-lg border p-5">
                  <span className="flex size-8 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
                    {i + 1}
                  </span>
                  <h3 className="pt-1 text-sm font-semibold md:text-base">{item.title}</h3>
                  <p className="text-sm leading-relaxed text-muted-foreground">{item.text}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        {/* Install: the whole pitch of a self-hosted product fits into three commands */}
        <section id="install" className="scroll-mt-24 border-t">
          <div className="mx-auto w-full max-w-5xl px-4 py-14 md:py-20">
            <h2 className="text-xl font-bold md:text-3xl">{l.install.title}</h2>
            <p className="mt-3 max-w-2xl text-sm text-muted-foreground md:text-base">{l.install.subtitle}</p>
            {/* one command per row: each code block gets the full width and never needs a
                horizontal scroll on any screen */}
            <div className="mt-8 grid gap-4">
              {[
                { icon: Code2, title: l.install.step1Title, code: l.install.step1Code },
                { icon: Terminal, title: l.install.step2Title, code: l.install.step2Code },
                { icon: Zap, title: l.install.step3Title, code: l.install.step3Code },
              ].map(({ icon: Icon, title, code }, i) => (
                <Card key={title}>
                  <CardContent className="flex flex-col gap-3 p-5">
                    <div className="flex items-center gap-2">
                      <span className="flex size-8 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">{i + 1}</span>
                      <Icon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
                      <h3 className="text-sm font-semibold md:text-base">{title}</h3>
                    </div>
                    <pre className="overflow-x-auto rounded-md border bg-muted/40 p-3 font-mono text-xs leading-relaxed">{code}</pre>
                  </CardContent>
                </Card>
              ))}
            </div>
            <p className="mt-4 text-sm text-muted-foreground">{l.install.openNote}</p>
          </div>
        </section>

        {/* CTA */}
        <section id="cta" className="scroll-mt-24 border-t bg-muted/40">
          <div className="mx-auto flex w-full max-w-5xl flex-col items-center gap-5 px-4 py-14 text-center md:py-20">
            <h2 className="max-w-xl text-xl font-bold md:text-3xl">{l.cta.title}</h2>
            <p className="max-w-lg text-sm text-muted-foreground md:text-base">{l.cta.subtitle}</p>
            <div className="flex flex-wrap items-center justify-center gap-3">
              {modePending || landingOnly ? null : (
                // no sign-in on a landing-only instance (and none while the mode is still
                // unknown): the repository is where the product starts
                <Button asChild size="lg" className="h-11 px-8">
                  <Link to={`/${routeLocale}/login`}>{l.cta.button}</Link>
                </Button>
              )}
              {/* the GitHub mark is the convention: a square icon button, the label
                  lives in the accessible name (the mark itself is decorative);
                  the same outline button on both instance kinds - the default
                  variant would make it a primary-blue square on the public landing */}
              <Button asChild size="icon" variant="outline" className="size-11" aria-label={l.cta.github}>
                <a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">
                  <GitHubIcon className="h-5 w-5" />
                </a>
              </Button>
              <Button asChild size="lg" variant="outline" className="h-11 px-8">
                <a href={BOOSTY_URL} target="_blank" rel="noopener noreferrer">
                  <BoostyIcon className="mr-2 h-5 w-5" />
                  {l.cta.support}
                </a>
              </Button>
              <Button asChild size="lg" variant="ghost" className="h-11 px-8">
                <Link to={`/${routeLocale}/indicators`}>{l.cta.browseIndicators}</Link>
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">{l.cta.note}</p>
          </div>
        </section>
      </main>

      {/* Mobile: auto height, links in two 44px rows and the copyright centered under them
          (a fixed h-16 row overflowed with the wrapped long legal labels); desktop: the old
          single 64px row with the copyright pinned right */}
      <footer className="border-t">
        <div className="mx-auto flex w-full max-w-5xl flex-col gap-1 px-4 py-2 text-xs text-muted-foreground md:h-16 md:flex-row md:flex-wrap md:items-center md:gap-x-6 md:gap-y-0 md:py-0">
          <nav className="grid grid-cols-2 gap-x-4 md:flex md:items-center md:gap-6">
            {[
              { to: `/${routeLocale}/legal/disclaimer`, label: t.footer.disclaimer },
              { to: `/${routeLocale}/legal/licenses`, label: t.footer.licenses },
              { to: GITHUB_URL, label: 'GitHub' },
              { to: BOOSTY_URL, label: t.footer.support },
            ].map(({ to, label }) => (
              to.startsWith('http')
                ? (
                  <a
                    key={to}
                    href={to}
                    target="_blank"
                    rel="noopener noreferrer"
                    className={cn('inline-flex min-h-11 items-center transition-colors hover:text-foreground md:min-h-0')}
                  >
                    {label}
                  </a>
                )
                : (
                  <Link
                    key={to}
                    to={to}
                    className={cn('inline-flex min-h-11 items-center transition-colors hover:text-foreground md:min-h-0')}
                  >
                    {label}
                  </Link>
                )
            ))}
          </nav>
          {/* text-center centers the copyright across the column on mobile: justify-self is
              a grid property and would be ignored in a flex container */}
          <span className="whitespace-nowrap py-2 text-center md:ml-auto md:py-0">© {new Date().getFullYear()} {t.common.appName}</span>
        </div>
      </footer>
    </div>
  );
}
