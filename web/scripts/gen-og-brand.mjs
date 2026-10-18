// Draws the fallback brand cards (public/og/brand.png and one per locale). The API redirects
// a link-preview picture it cannot render to them, so they are what a shared link shows when
// headless Chromium is unavailable; no page's og:image points at them directly any more.
//
// A one-off generator, not part of "npm run build": the PNG is committed (a messenger
// cannot wait for a browser to start before it unfurls a link), and the card only changes
// with the brand or the landing copy. Needs Chrome (or Chromium) on the machine:
//
//   npm run gen:og                       # all locales
//   SI_CHROME=/path/to/chromium npm run gen:og
//
// The layout mirrors the rendered cards (render/main.ts): headline block left, chart box
// right, so a fallback and a rendered picture look like one set.
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { copy, defaultLocale, i18n, locales } from './seo-lib.mjs';

const WIDTH = 1200;
const HEIGHT = 630;

// The chart's own colors (lib/chartCore.ts): the card must read as the app's picture
const UP = '#16a34a';
const DOWN = '#ef4444';
const TRIGGER_COLOR = '#f59e0b';
const INDICATOR = '#3b82f6';
const BORDER = '#e5e7eb';
const TEXT = '#475569';
const INK = '#0f172a';

const CHROME_CANDIDATES = [
  process.env.SI_CHROME,
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  '/Applications/Chromium.app/Contents/MacOS/Chromium',
  'google-chrome',
  'chromium',
].filter(Boolean);

function commandExists(command) {
  try {
    execFileSync('/bin/sh', ['-c', `command -v ${command}`], { stdio: 'ignore' });

    return true;
  } catch {
    return false;
  }
}

const chrome = CHROME_CANDIDATES.find((candidate) =>
  candidate.includes('/') ? existsSync(candidate) : commandExists(candidate));

if (!chrome) {
  console.error('gen:og: no Chrome/Chromium found (set SI_CHROME to the binary)');
  process.exit(1);
}

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

// The app icon, inlined (its own viewBox and size are replaced by the .mark box)
const MARK = readFileSync(path.join(root, 'public', 'favicon.svg'), 'utf8').replace(/^<svg/, '<svg aria-hidden="true"');

/**
 * A candle strip with an indicator line and the trigger arrow on the candle it fired on:
 * the same three signals the app's charts carry (direction colors, the amber marker), which
 * is all a brand card needs to say "charts that alert". Coordinates are the strip's own.
 */
function strip() {
  // [high, low, up] in a 0..100 price box, where a smaller number is higher on the picture.
  // The shape tells the product's one story: the price falls, an indicator line comes down
  // with it, a candle closes back through it - and that candle is where the alert fired.
  const candles = [
    [28, 44, true], [36, 54, false], [44, 62, false], [52, 70, false],
    [58, 76, false], [64, 84, false], [70, 88, false], [68, 86, true],
    [54, 72, true], [42, 58, true], [32, 48, true], [24, 42, true],
  ];

  // the candle that closed back above the line: the bottom of the move, and the trigger
  const TRIGGER_INDEX = 7;

  const X = (index) => 24 + index * 34;
  const Y = (value) => Math.round(value * 1.6) + 12;
  const mid = ([high, low]) => (Y(high) + Y(low)) / 2;

  const bodies = candles.map(([high, low, up], index) => {
    const color = up ? UP : DOWN;
    const x = X(index);

    return `<rect x="${x - 1.5}" y="${Y(high)}" width="3" height="${Y(low) - Y(high)}" fill="${color}"/>
        <rect x="${x - 13}" y="${Y(high) + 13}" width="26" height="${Math.max(8, Y(low) - Y(high) - 26)}" rx="3" fill="${color}"/>`;
  });

  // a real three-candle mean, so the line lags the price the way an SMA does instead of
  // cutting through the bodies at random
  const line = candles.map((candle, index) => {
    const window = candles.slice(Math.max(0, index - 2), index + 1);

    return `${X(index)},${Math.round(window.reduce((sum, c) => sum + mid(c), 0) / window.length)}`;
  });

  // The app's trigger arrow: under the candle it fired on, tip at its lower wick, pointing
  // up at the body. Anything else reads as a stray mark on the picture.
  const triggerX = X(TRIGGER_INDEX);
  const wickBottom = Y(candles[TRIGGER_INDEX][1]);

  return `<svg width="420" height="196" viewBox="0 0 420 196" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      ${bodies.join('\n        ')}
      <polyline points="${line.join(' ')}" fill="none" stroke="${INDICATOR}" stroke-width="3"
        stroke-linecap="round" stroke-linejoin="round" opacity="0.9"/>
      <path d="M ${triggerX} ${wickBottom + 8} L ${triggerX - 10} ${wickBottom + 24} L ${triggerX + 10} ${wickBottom + 24} Z" fill="${TRIGGER_COLOR}"/>
      <rect x="0" y="194" width="420" height="1" fill="${BORDER}"/>
    </svg>`;
}

function page(description, chips) {
  const esc = (value) => String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

  return `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
  :root { color-scheme: light; }
  html, body { margin: 0; padding: 0; width: ${WIDTH}px; height: ${HEIGHT}px; overflow: hidden; }
  body {
    background: #ffffff; color: ${INK};
    font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
    display: flex; align-items: center; gap: 56px;
    padding: 0 72px; box-sizing: border-box;
  }
  .copy { flex: 1 1 auto; min-width: 0; }
  .brand { display: flex; align-items: center; gap: 24px; }
  .mark { width: 88px; height: 88px; flex: 0 0 auto; border-radius: 20px; overflow: hidden;
    box-shadow: 0 1px 0 ${BORDER}, 0 10px 26px rgba(15,23,42,.10); }
  .mark svg { width: 88px; height: 88px; display: block; }
  /* one line: a brand name broken in two reads as a broken layout, and the preview is
     shown ~400px wide, where every pixel of size counts */
  .name { font-size: 46px; font-weight: 700; letter-spacing: -1.4px; line-height: 1.05; white-space: nowrap; }
  .tag { font-size: 26px; line-height: 1.45; color: ${TEXT}; margin-top: 28px; max-width: 500px; }
  .chips { display: flex; flex-wrap: nowrap; gap: 10px; margin-top: 34px; }
  .chip { font-size: 18px; font-weight: 600; color: ${TEXT}; padding: 7px 15px; white-space: nowrap;
    border: 1px solid ${BORDER}; border-radius: 999px; background: #f8fafc; }
  .chart { flex: 0 0 auto; border: 1px solid ${BORDER}; border-radius: 18px; padding: 24px 22px 18px;
    box-shadow: 0 12px 32px rgba(15,23,42,.06); }
  .chart .title { font-size: 17px; font-weight: 600; font-family: ui-monospace, monospace;
    color: ${TEXT}; margin: 0 0 14px 4px; letter-spacing: .2px; }
</style>
</head>
<body>
  <div class="copy">
    <div class="brand">
      <div class="mark">${MARK}</div>
      <div class="name">${esc(copy.siteName)}</div>
    </div>
    <div class="tag">${esc(description)}</div>
    <div class="chips">${chips.map((chip) => `<span class="chip">${esc(chip)}</span>`).join('')}</div>
  </div>
  <div class="chart">
    <div class="title">AAPL · SMA(20) · 1D</div>
    ${strip()}
  </div>
</body>
</html>
`;
}

const outDir = path.join(root, 'public', 'og');

mkdirSync(outDir, { recursive: true });

const tmp = mkdtempSync(path.join(os.tmpdir(), 'si-og-'));
const written = [];

for (const locale of locales) {
  const seo = i18n[locale].landing.seo;
  const chips = [...copy.channelChips[locale]];

  // The default locale's file carries the plain name too: the API redirects an unrenderable
  // indicator card there without knowing the visitor's language
  const names = locale === defaultLocale ? ['brand.png', `brand-${locale}.png`] : [`brand-${locale}.png`];
  const htmlPath = path.join(tmp, `${locale}.html`);

  writeFileSync(htmlPath, page(seo.description, chips));

  for (const name of names) {
    const target = path.join(outDir, name);

    execFileSync(chrome, [
      '--headless=new',
      '--disable-gpu',
      '--hide-scrollbars',
      '--force-device-scale-factor=1',
      '--default-background-color=FFFFFFFF',
      `--window-size=${WIDTH},${HEIGHT}`,
      `--screenshot=${target}`,
      `file://${htmlPath}`,
    ], { stdio: 'pipe' });

    if (!existsSync(target)) throw new Error(`${name}: Chrome wrote no screenshot`);

    written.push(`${name} ${Math.round(readFileSync(target).length / 1024)} kB`);
  }
}

rmSync(tmp, { recursive: true, force: true });

console.log(`gen:og: ${written.length} cards at ${WIDTH}x${HEIGHT} via ${path.basename(chrome)}`);
console.log(`  ${written.join('\n  ')}`);
