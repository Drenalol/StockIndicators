// Pulls the indicator descriptions the SEO shells are generated from out of the API and
// stores them as a committed snapshot (frontend/seo/indicator-descriptions.json).
//
// The descriptions are the backend's IndicatorHint texts: the documentation prose lives in
// template attributes, not in the frontend's i18n catalogs, and a build must not need a
// running API. So the texts are snapshotted here and reviewed as a normal diff, and
// gen-seo-pages.mjs reads the snapshot. An indicator's description only ever changes with
// its template, which is also when this has to be re-run:
//
//   npm run sync:seo                       # from prod
//   SI_API_URL=https://localhost:5051 npm run sync:seo
//
// It also fails (exit 1) when the snapshot was already behind the API, so it can be run as
// a gate: SEO_CHECK=1 npm run sync:seo. What the built pages look like is checked offline
// by "npm run check:seo".
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const target = path.join(root, 'seo', 'indicator-descriptions.json');
const baseUrl = (process.env.SI_API_URL ?? 'https://localhost:5051').replace(/\/+$/, '');

// The route locales are the backend's supported language codes (UserLocale.SupportedLanguages)
const LOCALES = ['en', 'ru', 'es', 'it', 'de', 'fr', 'pt-BR', 'uk', 'be'];

// A plain curl/node request is not what the site expects to see; the catalog is anonymous,
// this only keeps the edge from treating the sync as a bot it must not answer
const HEADERS = { 'User-Agent': 'stock-indicators-seo-sync/1.0', Accept: 'application/json' };

/** Unwraps the API's response envelope ({ statusCode, result | error }). */
const unwrap = async (response) => {
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);

  const body = await response.json();
  const result = body && typeof body === 'object' && 'result' in body ? body.result : body;

  if (!Array.isArray(result)) throw new Error(`unexpected response: ${JSON.stringify(body).slice(0, 160)}`);

  return result;
};

const fetchCatalog = async (locale) => {
  const response = await fetch(`${baseUrl}/api/indicator/catalog?lang=${encodeURIComponent(locale)}`, { headers: HEADERS });
  const items = await unwrap(response);

  // Alphabetical: the API orders premium-first, which reshuffles the whole file on every
  // premium change and makes the review diff unreadable
  return Object.fromEntries(
    items
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((item) => [item.name, item.description ?? '']),
  );
};

const previous = (() => {
  try {
    return JSON.parse(readFileSync(target, 'utf8'));
  } catch {
    return null;
  }
})();

const descriptions = {};

for (const locale of LOCALES) {
  descriptions[locale] = await fetchCatalog(locale);
  console.log(`${locale}: ${Object.keys(descriptions[locale]).length} indicators`);
}

const snapshot = {
  _comment: 'Snapshot of GET /api/indicator/catalog?lang=<locale> (the backend IndicatorHint texts) that the static link-preview pages are generated from. Regenerate with "npm run sync:seo" whenever an indicator description changes.',
  source: baseUrl,
  locales: LOCALES,
  descriptions,
};

const next = `${JSON.stringify(snapshot, null, 2)}\n`;

if (previous && previous.descriptions && JSON.stringify(previous.descriptions) === JSON.stringify(descriptions)) {
  console.log(`\n${path.relative(root, target)} is up to date (${LOCALES.length} locales)`);
  process.exit(0);
}

mkdirSync(path.dirname(target), { recursive: true });
writeFileSync(target, next);console.log(`\n${path.relative(root, target)} written (${next.length} bytes)`);

if (process.env.SEO_CHECK === '1') {
  console.error('the snapshot is behind the API - commit the regenerated seo/indicator-descriptions.json');
  process.exit(1);
}
