// Writes the static HTML shells the link previews (Telegram, WhatsApp, Discord, VK) and
// search engines read: dist/<locale>/index.html, dist/<locale>/indicators/index.html,
// dist/<locale>/indicators/<NAME>/index.html and dist/sitemap.xml.
//
// Why files instead of a runtime endpoint: a messenger crawler does not run JS and reads
// the meta tags out of the raw HTML of the exact URL it was given, while the app is an SPA
// whose shell is the same for every path. nginx answers such a URL from a directory's
// index.html ("try_files $uri $uri/index.html ...", see the note in the prod config), so a
// file at dist/ru/indicators/RSI/index.html is served for /ru/indicators/RSI as is: one
// document for the crawler and for the user, and the SPA boots out of the very same shell
// and renders the real page.
//
// Runs after "vite build" (it reuses the shell of this build, hashed asset tags included),
// as the last step of "npm run build". "npm run check:seo" verifies the output, "npm run
// sync:seo" refreshes the indicator descriptions the pages are built from.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import {
  copy,
  dist,
  esc,
  i18n,
  defaultLocale,
  indicatorDescription,
  landingAlt,
  landingImage,
  locales,
  ogLocale,
  pageEntries,
  summarize,
} from './seo-lib.mjs';

const shellPath = path.join(dist, 'index.html');

if (!existsSync(shellPath)) {
  throw new Error('dist/index.html is missing - run "vite build" before "node scripts/gen-seo-pages.mjs"');
}

// || and not ??: a docker build-arg left unset arrives as an empty string, and an empty base
// would silently strip the host from every canonical instead of falling back to copy.json
const baseUrl = (process.env.SEO_BASE_URL || copy.baseUrl).replace(/\/+$/, '');

// The shell vite just wrote
const shell = readFileSync(shellPath, 'utf8');

/**
 * Writes one page: the built shell with its title, description, language and meta block
 * rewritten, and returns its path relative to dist (its URL is the directory).
 *
 * canonical and hreflang are emitted only when the caller knows the URL the file is served
 * at (urlPath). The root shell passes null on purpose: that file is also the fallback
 * document of every unknown path, and a canonical there would declare unrelated URLs
 * duplicates of the landing.
 */
function buildPage({ locale, file, urlPath, title, description, image, imageAlt }) {
  const tags = [
    '<meta property="og:type" content="website">',
    `<meta property="og:site_name" content="${esc(copy.siteName)}">`,
    `<meta property="og:title" content="${esc(title)}">`,
    `<meta property="og:description" content="${esc(description)}">`,
    `<meta property="og:locale" content="${ogLocale(locale)}">`,
    `<meta property="og:image" content="${esc(image)}">`,
    '<meta property="og:image:width" content="1200">',
    '<meta property="og:image:height" content="630">',
    `<meta property="og:image:alt" content="${esc(imageAlt)}">`,
    // Telegram, WhatsApp and VK read og:*, Google reads the description and the alternates,
    // Twitter/X and Discord read twitter:* - Discord is the one client that ignores
    // og:image while twitter:card is absent, so both sets are emitted
    '<meta name="twitter:card" content="summary_large_image">',
    `<meta name="twitter:title" content="${esc(title)}">`,
    `<meta name="twitter:description" content="${esc(description)}">`,
    `<meta name="twitter:image" content="${esc(image)}">`,
  ];

  if (urlPath) {
    tags.push(`<link rel="canonical" href="${esc(baseUrl + urlPath)}">`);

    // the same page in another language: the locale is the first path segment, so the
    // alternates swap it ("/ru/indicators/RSI" -> "/de/indicators/RSI", "/ru" -> "/de")
    const rest = urlPath.slice(`/${locale}`.length);

    for (const alternate of locales) {
      tags.push(`<link rel="alternate" hreflang="${alternate}" href="${esc(`${baseUrl}/${alternate}${rest}`)}">`);
    }

    // the language-neutral entry: "/" localizes the visitor by their profile language
    tags.push(`<link rel="alternate" hreflang="x-default" href="${esc(`${baseUrl}/`)}">`);
  }

  const rewrite = (html, regexp, replacement, what) => {
    if (!regexp.test(html)) throw new Error(`${file}: ${what} not found in the built shell`);

    // a function, not the string: String.replace interprets "$&" and "$'" in a replacement,
    // and a price or a currency in some future copy would silently corrupt the page
    return html.replace(regexp, () => replacement);
  };

  // dropping a previous block first: running this twice on one dist must not double it
  let html = shell.replace(/<!--seo:generated-->[\s\S]*?<!--seo:end-->/g, '');

  html = rewrite(html, /<title>[\s\S]*?<\/title>/, `<title>${esc(title)}</title>`, 'the <title> element');
  html = rewrite(html, /<meta name="description" content="[^"]*"/, `<meta name="description" content="${esc(description)}"`, 'the description meta');
  html = rewrite(html, /<html lang="[^"]*"/, `<html lang="${locale}"`, 'the html lang attribute');

  const block = `\n    <!--seo:generated-->\n    ${tags.join('\n    ')}\n    <!--seo:end-->\n  `;

  html = rewrite(html, /<\/head>/, `${block}</head>`, 'the closing head tag');

  const target = path.join(dist, file);

  mkdirSync(path.dirname(target), { recursive: true });
  writeFileSync(target, html);

  // a page that quietly lost its preview is the failure mode this script exists to prevent
  if (!readFileSync(target, 'utf8').includes('<meta property="og:image"')) {
    throw new Error(`${file}: the written page carries no og:image`);
  }

  return path.relative(dist, target).split(path.sep).join('/');
}

const fallback = i18n[defaultLocale];

// The shell itself is what a crawler sees at "/": the same picture as the landing page, in
// the fallback language, with no canonical (see buildPage)
buildPage({
  locale: defaultLocale,
  file: 'index.html',
  urlPath: null,
  title: fallback.landing.seo.title,
  description: summarize(fallback.landing.seo.description),
  image: landingImage(baseUrl, defaultLocale),
  imageAlt: landingAlt(defaultLocale),
});

const pages = pageEntries(baseUrl).map((page) => buildPage(page));

// Sanity of the derived set: an indicator with no localized name or description would still
// produce a page (the catalog copy), but it must never happen silently
const untranslated = pages.filter((page) => page.kind === 'indicator' && !indicatorDescription(page.name, page.locale));

if (untranslated.length > 0) {
  const sample = [...new Set(untranslated.map((page) => page.name))].join(', ');
  const missing = [...new Set(untranslated.map((page) => page.locale))].join(', ');

  console.warn(`seo: ${untranslated.length} indicator pages fall back to the English hint (indicators: ${sample}; locales: ${missing}) - "npm run sync:seo" refreshes the snapshot`);
}

// A plain <loc> list: the language alternates are declared in the pages themselves
// (rel=alternate in the head), repeating all of them per URL here would only multiply the
// size. The generated pages are listed, "/" is not - it is a redirect, not a page.
const urls = pages.map((file) => `/${file.replace(/index\.html$/, '')}`.replace(/\/$/, ''));

writeFileSync(
  path.join(dist, 'sitemap.xml'),
  `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls
    .map((url) => `  <url><loc>${esc(baseUrl + url)}</loc></url>`)
    .join('\n')}\n</urlset>\n`,
);

console.log(`seo: ${pages.length + 1} pages (${locales.length} locales x ${pages.length / locales.length} routes), sitemap.xml with ${urls.length} urls, base ${baseUrl}`);
