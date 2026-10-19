// Verifies the static link-preview pages of a built dist (npm run check:seo): every page
// exists, declares its own title, description, og:image and canonical, the language
// alternates point at each other, the sitemap lists all of them, and the description
// snapshot behind the indicator pages is complete.
//
// It reads dist only (no network), so it runs after "vite build" on any machine. Run it
// against a production dist to catch a build that shipped a page without a preview.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { brandImage, copy, defaultLocale, descriptions, dist, indicators, locales, ogLocale, pageEntries } from './seo-lib.mjs';

// the same fallback gen-seo-pages.mjs applies (an unset build-arg is an empty string)
const baseUrl = (process.env.SEO_BASE_URL || copy.baseUrl).replace(/\/+$/, '');

const problems = [];
const pages = pageEntries(baseUrl);

// every path the site publishes: an alternate or a sitemap entry must name one of these
const knownPaths = new Set(pages.map((page) => page.urlPath));

if (!existsSync(path.join(dist, 'index.html'))) {
  console.error(`check:seo: dist/index.html is missing - build the frontend first`);
  process.exit(1);
}

const readPage = (file) => readFileSync(path.join(dist, file), 'utf8');

/** The content of one meta/link tag the generator writes, or null. */
const tag = (html, selector) => {
  const match = new RegExp(`<meta [^>]*${selector}[^>]*content="([^"]*)"`).exec(html) ??
    new RegExp(`<link [^>]*${selector}[^>]*href="([^"]*)"`).exec(html);

  // the generator escapes every attribute value, so an ampersand arrives as &amp; -
  // a picture URL (lang=..&v=..) must compare equal to the value it was built from
  return match ? match[1].replace(/&amp;/g, '&') : null;
};

for (const page of pages) {
  if (!existsSync(path.join(dist, page.file))) {
    problems.push(`${page.file}: the page is missing (rebuild: npm run build)`);
    continue;
  }

  const html = readPage(page.file);

  const checks = [
    ['<title>', /<title>([^<]*)<\/title>/.exec(html)?.[1], page.title],
    ['og:image', tag(html, 'property="og:image"'), page.image],
    ['og:locale', tag(html, 'property="og:locale"'), ogLocale(page.locale)],
    ['canonical', tag(html, 'rel="canonical"'), baseUrl + page.urlPath],
    ['lang', /<html lang="([^"]*)"/.exec(html)?.[1], page.locale],
  ];

  for (const [what, found, want] of checks) {
    if (found !== want) problems.push(`${page.file}: ${what} is ${JSON.stringify(found)}, expected ${JSON.stringify(want)}`);
  }

  if (!html.includes('<meta name="twitter:card"')) problems.push(`${page.file}: twitter:card is missing (Discord shows no picture without it)`);
  if (!html.includes('hreflang="x-default"')) problems.push(`${page.file}: the x-default alternate is missing`);
  if (html.includes('/dashboard')) problems.push(`${page.file}: the page leaks a dashboard path into its preview`);

  // every alternate must name an existing page in its own language: an hreflang pair that
  // points at a path nothing serves is worse than no hreflang at all
  for (const [, hreflang, href] of html.matchAll(/<link rel="alternate" hreflang="([^"]+)" href="([^"]+)">/g)) {
    if (hreflang === 'x-default') continue;

    if (!locales.includes(hreflang)) {
      problems.push(`${page.file}: hreflang="${hreflang}" is not a route locale`);
      continue;
    }

    const target = href.slice(baseUrl.length);

    if (!knownPaths.has(target)) problems.push(`${page.file}: the ${hreflang} alternate points at ${target}, which is no page`);
    else if (!target.startsWith(`/${hreflang}`)) problems.push(`${page.file}: the ${hreflang} alternate points at ${target}, which is another language`);
  }

  // the shell must still boot the SPA: the asset tags of this build are what makes the
  // generated document a page and not a dead end
  if (!/<script type="module"[^>]*src="\/assets\//.test(html)) problems.push(`${page.file}: the built app entry is gone, the page would render nothing`);
}

// the fallback document a crawler sees at "/": branded, and deliberately without a
// canonical, because it is also the document of every unknown path
const shell = readPage('index.html');

if (!shell.includes('property="og:image"')) problems.push('index.html: the root shell carries no og:image, a shared "/" previews as text');
if (shell.includes('rel="canonical"')) problems.push('index.html: the root shell declares a canonical, which would make unrelated URLs duplicates of the landing');

const missing = [];

for (const locale of locales) {
  for (const name of indicators) {
    if (!descriptions.descriptions?.[locale]?.[name]) missing.push(`${locale}/${name}`);
  }
}

if (missing.length > 0) {
  problems.push(`seo/indicator-descriptions.json: ${missing.length} descriptions are missing (${missing.slice(0, 6).join(', ')}${missing.length > 6 ? ', ...' : ''}) - "npm run sync:seo"`);
}

const sitemapPath = path.join(dist, 'sitemap.xml');

if (!existsSync(sitemapPath)) {
  problems.push('sitemap.xml is missing');
} else {
  const sitemap = readFileSync(sitemapPath, 'utf8');
  const unlisted = pages.filter((page) => !sitemap.includes(`<loc>${baseUrl}${page.urlPath}</loc>`)).map((page) => page.urlPath);

  if (unlisted.length > 0) problems.push(`sitemap.xml: ${unlisted.length} pages are not listed (first: ${unlisted[0]})`);
  if (sitemap.includes(`${baseUrl}/<`)) problems.push('sitemap.xml: a malformed url slipped in');
}

// The fallback pictures. No page points at them any more (every picture is rendered by the
// API now), but the API redirects to them when it cannot render at all, so they must be in
// the build: nginx answers a missing one with 404 (the /og/ location), which would make the
// degradation a dead end instead of a picture. And "the URL answers 200" proves nothing on
// this host - the SPA shell is a 200 text/html - so the file is read here instead.
const pngSize = (file) => {
  const bytes = readFileSync(file);

  return bytes.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && bytes.subarray(12, 16).toString('ascii') === 'IHDR'
    ? [bytes.readUInt32BE(16), bytes.readUInt32BE(20)]
    : null;
};

for (const locale of [...locales, defaultLocale]) {
  const pathname = new URL(brandImage(baseUrl, locale)).pathname;
  const file = path.join(dist, pathname);

  if (!existsSync(file)) {
    problems.push(`${pathname}: the fallback card is not in the build (npm run gen:og), and the API's fallback redirect would 404`);
    continue;
  }

  const size = pngSize(file);

  if (!size) problems.push(`${pathname}: is not a PNG a messenger can read`);
  else if (size[0] !== 1200 || size[1] !== 630) problems.push(`${pathname}: is ${size[0]}x${size[1]}, the messengers were told 1200x630`);
}

// Every page's picture is rendered by the API for its language: a picture URL without the
// language would show a foreign-language card, and one that is not an /api/share/ URL would
// be a static file no code writes any more
for (const page of pages) {
  const image = new URL(page.image);

  if (!image.pathname.startsWith('/api/share/')) problems.push(`${page.file}: og:image points at ${image.pathname}, which nothing renders`);
  else if (image.searchParams.get('lang') !== page.locale) problems.push(`${page.file}: og:image asks for lang=${image.searchParams.get('lang')} on a ${page.locale} page`);
}

if (problems.length > 0) {
  console.error(`check:seo: ${problems.length} problems in ${pages.length + 1} pages`);

  for (const problem of problems.slice(0, 25)) console.error(`  ${problem}`);
  if (problems.length > 25) console.error(`  ... ${problems.length - 25} more`);

  process.exit(1);
}

console.log(`check:seo: ${pages.length + 1} pages ok (${locales.length} locales x ${indicators.length + 2} routes), ${pages.length} sitemap urls`);
