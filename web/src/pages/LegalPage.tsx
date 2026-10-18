import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation, SUPPORTED_LOCALES, type Locale } from '@/i18n';
import { PublicPageHeader } from '@/components/layout/PublicPageHeader';
import NotFoundPage from '@/pages/NotFoundPage';
import { onExternalLinkClick } from '@/lib/externalLink';
import type { LicenseBlock } from '@/lib/openSourceLicenses';

const LEGAL_SLUGS = ['disclaimer', 'licenses'] as const;
type LegalSlug = typeof LEGAL_SLUGS[number];

const LICENSE_TITLES: Record<string, string> = {
  'MIT': 'MIT License',
  'ISC': 'ISC License',
  'Apache-2.0': 'Apache License 2.0',
  '0BSD': 'BSD Zero Clause License',
};

function LicenseBlockSection({ block }: { block: LicenseBlock }) {
  return (
    <section>
      <h2 className="text-base font-semibold">{LICENSE_TITLES[block.license] ?? block.license}</h2>
      <ul className="mt-2 space-y-1 text-sm text-muted-foreground">
        {block.packages.map((pkg) => (
          <li key={pkg.name}>
            {pkg.url ? (
              <a href={pkg.url} target="_blank" rel="noreferrer" onClick={onExternalLinkClick} className="text-primary hover:underline underline-offset-2">
                {pkg.name}
              </a>
            ) : (
              pkg.name
            )}
            {pkg.version ? <span className="text-xs"> {pkg.version}</span> : null}
          </li>
        ))}
      </ul>
      {block.text ? (
        <pre className="mt-3 whitespace-pre-wrap rounded-md bg-muted p-4 font-sans text-xs leading-relaxed text-muted-foreground">
          {block.text}
        </pre>
      ) : block.textUrl ? (
        <p className="mt-2 text-sm text-muted-foreground">
          <a href={block.textUrl} target="_blank" rel="noreferrer" onClick={onExternalLinkClick} className="text-primary hover:underline underline-offset-2">
            {block.license}
          </a>
        </p>
      ) : null}
    </section>
  );
}

/**
 * Public legal documents, linked from the landing footer and the settings
 * card. Lives outside the authenticated MainLayout chrome on purpose, so an
 * anonymous visitor can read the terms before signing up; the slim header
 * back arrow pops the browser history (landing only as the deep-link
 * fallback). Like the landing, it is a regular scrolling page, not an app page.
 */
export default function LegalPage() {
  const { t, locale } = useTranslation();
  const { slug, locale: pathLocale } = useParams();
  const [licenses, setLicenses] = useState<LicenseBlock[] | null>(null);

  useEffect(() => {
    if (slug !== 'licenses' || licenses) return;
    let cancelled = false;
    import('@/lib/openSourceLicenses').then((m) => {
      if (!cancelled) setLicenses(m.LICENSE_GROUPS);
    });
    return () => {
      cancelled = true;
    };
  }, [slug, licenses]);

  if (!slug || !LEGAL_SLUGS.includes(slug as LegalSlug)) {
    return <NotFoundPage />;
  }
  const content = t.legal[slug as LegalSlug];

  const routeLocale = (SUPPORTED_LOCALES.includes((pathLocale ?? '') as Locale)
    ? pathLocale
    : locale) as Locale;

  return (
    <div className="min-h-[100dvh] bg-background text-foreground flex flex-col">
      <PublicPageHeader backFallback={`/${routeLocale}`} />

      <main className="mx-auto w-full max-w-3xl px-3 md:px-4 py-8 pb-[calc(2rem+var(--safe-b))]">
        <h1 className="text-2xl font-bold">{content.title}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{content.updated}</p>

        <div className="mt-8 space-y-8">
          {content.sections.map((section, i) => (
            <section key={i}>
              <h2 className="text-base font-semibold">{section.heading}</h2>
              <div className="mt-2 space-y-2 text-sm leading-relaxed text-muted-foreground">
                {section.paragraphs.map((paragraph, j) => (
                  <p key={j}>{paragraph}</p>
                ))}
              </div>
            </section>
          ))}
          {slug === 'licenses' && licenses
            ? licenses.map((block) => <LicenseBlockSection key={block.license + block.packages[0].name} block={block} />)
            : null}

          {slug === 'licenses' && (
            <section>
              <h2 className="text-base font-semibold">{t.legal.logos.heading}</h2>
              {/* The free Logo.dev plan requires a visible back link; this page is public, which satisfies its placement rules.
                  The exact href/text of their attribution guide, without target/rel: their verifier rejects a
                  link that strips the referrer (rel="noreferrer") or whose href host is www.logo.dev.
                  The click handler is native-only and changes no attribute: inside the shell the tap would otherwise
                  navigate the WebView off our origin (the host is not in allowNavigation) instead of leaving the page */}
              <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                <a href="https://logo.dev" onClick={onExternalLinkClick} className="text-primary hover:underline underline-offset-2">
                  {t.legal.logos.link}
                </a>
              </p>
            </section>
          )}
        </div>
      </main>
    </div>
  );
}
