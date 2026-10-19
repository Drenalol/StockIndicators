// Shared data and helpers of the SEO scripts (gen-seo-pages.mjs writes the static
// link-preview pages, check-seo.mjs verifies them, sync-seo-descriptions.mjs refreshes the
// indicator description snapshot). Kept in one place so the three of them cannot disagree
// about what the set of pages is.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const dist = path.join(root, 'dist');

const read = (relative) => readFileSync(path.join(root, relative), 'utf8');

/**
 * Reads a string array literal out of an app source file. The two lists the pages are built
 * from live in the app, not here: the route locales and the indicator slugs must match the
 * SPA exactly, and a slug that is not the enum's wire name would generate a page that no
 * route ever serves.
 */
export const readArray = (relative, marker) => {
  const match = new RegExp(`${marker}[^=]*=\\s*\\[([\\s\\S]*?)\\]`).exec(read(relative));

  if (!match) throw new Error(`${relative}: ${marker} not found`);

  return [...match[1].matchAll(/'([^']+)'/g)].map((found) => found[1]);
};

export const copy = JSON.parse(read(path.join('seo', 'copy.json')));
export const locales = readArray(path.join('src', 'i18n', 'types.ts'), 'SUPPORTED_LOCALES');
export const indicators = readArray(path.join('src', 'i18n', 'indicatorTranslations.ts'), 'KNOWN_INDICATORS');

/** The description snapshot; missing file is not fatal: the pages fall back to catalog copy. */
export const descriptions = (() => {
  try {
    return JSON.parse(read(path.join('seo', 'indicator-descriptions.json')));
  } catch {
    return { descriptions: {} };
  }
})();

export const i18n = Object.fromEntries(locales.map((locale) => [locale, JSON.parse(read(path.join('src', 'i18n', `${locale}.json`)))]));

export const defaultLocale = copy.defaultLocale;

export const esc = (value) =>
  String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

/**
 * og:description is shown in two or three lines and cut by the client without a sign, so
 * the cut happens here, on a word boundary. Newlines would break the attribute's shape.
 */
export const summarize = (text) => {
  const flat = String(text ?? '').replace(/\s+/g, ' ').trim();
  const budget = copy.descriptionLength;

  if (flat.length <= budget) return flat;

  const cut = flat.lastIndexOf(' ', budget);

  return `${flat.slice(0, cut > 0 ? cut : budget).replace(/[,;:.]$/, '')}…`;
};

/**
 * Mirrors src/i18n/indicatorTranslations.ts getIndicatorFullNameWithAbbr: the localized name
 * with the wire abbreviation after it ("Простая скользящая средняя | SMA"), the short name
 * alone where the two coincide (English).
 */
export const indicatorTitle = (name, locale) => {
  const localized = i18n[locale].indicators.names?.[name] ?? name;

  return localized === name ? name : `${localized} | ${name}`;
};

/** The locale's og:locale code (Open Graph wants the locale_Country form). */
export const ogLocale = (locale) => copy.locales[locale]?.ogLocale ?? locale;

/**
 * The brand card of a language: a raster file the API redirects to when it cannot render a
 * picture at all. No page points at it any more (every page's picture is rendered), but it
 * must stay in the build - the fallback of an unrenderable card is this file, and nginx
 * answers a missing one with 404 on purpose, so a redirect would land on a dead end.
 * The default locale's file carries the plain name: the API does not know the visitor's
 * language when it degrades, so that name must exist.
 */
export const brandImage = (baseUrl, locale) =>
  `${baseUrl}${locale === defaultLocale ? copy.brandImage : `/og/brand-${locale}.png`}`;

/**
 * The stamp of the build the picture URLs carry. A messenger caches a preview picture by
 * its URL and not by the page that named it, so every build must hand the crawlers a new
 * URL: the brand card the messengers cached while the renderer was down would otherwise
 * stick to every page pointing at the old URL long after the real renders are back. Read
 * lazily from dist so the generator and the checker of one dist always agree about it; a
 * dist built before the stamp existed simply carries no version.
 */
const imageVersion = () => {
  try {
    return readFileSync(path.join(dist, 'og-version'), 'utf8').trim();
  } catch {
    return '';
  }
};

const imageParams = (locale) => `lang=${locale}${imageVersion() ? `&v=${imageVersion()}` : ''}`;

/**
 * The landing page and the indicator catalog are pictured with a real example chart (real
 * candles, a candle the production handler triggers on) under the brand name - not with a
 * drawn decoration. Which indicator that is belongs to the deployment
 * (ShareImage:LandingIndicator), never to the request: the endpoint is anonymous and
 * Cloudflare keeps its answers for a week.
 */
export const landingImage = (baseUrl, locale) => `${baseUrl}/api/share/landing/image.png?${imageParams(locale)}`;

export const indicatorImage = (baseUrl, name, locale) => `${baseUrl}/api/share/indicators/${name}/image.png?${imageParams(locale)}`;

export const brandAlt = (locale) => copy.brandImageAlt[locale] ?? copy.brandImageAlt[defaultLocale];

/** The alt of the landing/catalog picture: a real chart, not a logo. */
export const landingAlt = (locale) => copy.landingImageAlt[locale] ?? copy.landingImageAlt[defaultLocale];

export const indicatorAlt = (locale) => copy.imageAlt[locale] ?? copy.imageAlt[defaultLocale];

/** The description of an indicator page: the hint, falling back to English, then to copy. */
export const indicatorDescription = (name, locale) =>
  descriptions.descriptions?.[locale]?.[name] || descriptions.descriptions?.[defaultLocale]?.[name] || null;

/**
 * Every page the site publishes, as { locale, file, urlPath, title, description, image,
 * imageAlt }. The root shell is not here: it is the language-neutral fallback document of
 * every unknown path and is built separately, without canonical or alternates.
 */
export const pageEntries = (baseUrl) => {
  const pages = [];

  for (const locale of locales) {
    const t = i18n[locale];

    pages.push({
      kind: 'landing',
      locale,
      file: `${locale}/index.html`,
      urlPath: `/${locale}`,
      title: t.landing.seo.title,
      description: summarize(t.landing.seo.description),
      image: landingImage(baseUrl, locale),
      imageAlt: landingAlt(locale),
    });

    pages.push({
      kind: 'catalog',
      locale,
      file: `${locale}/indicators/index.html`,
      urlPath: `/${locale}/indicators`,
      title: `${t.indicators.title}${copy.titleSuffix}`,
      description: summarize(t.indicators.publicHint),
      image: landingImage(baseUrl, locale),
      imageAlt: landingAlt(locale),
    });

    for (const name of indicators) {
      pages.push({
        kind: 'indicator',
        locale,
        name,
        file: `${locale}/indicators/${name}/index.html`,
        urlPath: `/${locale}/indicators/${name}`,
        title: `${indicatorTitle(name, locale)}${copy.titleSuffix}`,
        // an indicator the snapshot does not know yet (added after the last "npm run
        // sync:seo") still gets a page: the catalog hint says what the page is
        description: summarize(indicatorDescription(name, locale) || t.indicators.publicHint),
        image: indicatorImage(baseUrl, name, locale),
        imageAlt: indicatorAlt(locale),
      });
    }
  }

  return pages;
};
