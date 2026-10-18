// The server-side chart image entry: the Playwright renderer loads the built single-file
// page (dist/render/renderer.html), injects window.__SI_REQUEST__ before this script runs,
// and screenshots #si-root once __SI_DONE__ is set. It draws the alert picture through the
// very same chart core the web app renders (lib/chartCore), so the account's chart options
// and the chart's look cannot drift apart between an alert and the app.

import { buildChartCore } from '@/lib/chartCore';
import type { ChartData } from '@/types/contracts';

/**
 * The two shapes the page draws. "alert" is the bare chart the channels have always sent;
 * "card" is the link-preview picture (og:image): the same chart under a headline, sized the
 * way messengers ask for.
 */
export type RenderMode = 'alert' | 'card';

export interface RenderRequest {
  /** The same ChartData JSON the /chart endpoints serve (display re-stamped times) */
  data: ChartData;
  locale: string;
  labels: { o: string; h: string; l: string; c: string; vol: string };
  /** The recipient's persisted chart preferences (magnet is inert on a still image) */
  preferences: { tooltip: boolean; magnet: boolean };
  /** Omitted = "alert": an older API build keeps rendering the alert picture off this page */
  mode?: RenderMode;
  /** CSS pixels of the picture; the browser viewport is opened at this size at `scale` */
  size?: { width: number; height: number; scale: number };
  /** Card only: headline (the indicator name) and the line under it (its description) */
  title?: string | null;
  subtitle?: string | null;
  /**
   * Card only: the small brand line over the headline. Absent = the brand (an older API
   * build predates the field); explicit null = draw no line, for a card whose headline
   * already is the brand name.
   */
  eyebrow?: string | null;
}

declare global {
  interface Window {
    __SI_REQUEST__?: RenderRequest;
    __SI_DONE__?: boolean;
    __SI_ERROR__?: string;
  }
}

// The card's palette mirrors the light theme the chart reads from the page's CSS variables
const CARD = {
  eyebrow: 'STOCK INDICATORS',
  background: '#ffffff',
  title: '#0f172a',
  text: '#64748b',
  border: '#e5e7eb',
};

/**
 * The headline block of a link-preview card: brand, indicator name, its description. The
 * text is server-derived (an enum member and an indicator hint), never the visitor's input,
 * and goes in through textContent, so nothing here can be injected as markup.
 */
function buildCardHead(title: string, subtitle: string | null, eyebrow: string | null): HTMLElement {
  const head = document.createElement('div');
  head.style.cssText = 'flex:0 0 auto;padding:44px 48px 24px;';

  if (eyebrow) {
    const line = document.createElement('div');
    line.style.cssText = `font-size:15px;font-weight:600;letter-spacing:2.5px;color:${CARD.text};`;
    line.textContent = eyebrow;
    head.appendChild(line);
  }

  const headline = document.createElement('div');
  // the gap the eyebrow used to leave is the headline's own; without it the block starts
  // at the card's padding instead of half a line lower
  headline.style.cssText = `margin-top:${eyebrow ? '10px' : '0'};font-size:46px;line-height:1.15;font-weight:700;letter-spacing:-0.5px;color:${CARD.title};`;
  headline.textContent = title;
  head.appendChild(headline);

  if (subtitle) {
    const line = document.createElement('div');
    // two lines and an ellipsis: the API already cuts the text on a word boundary, this
    // only guarantees a long language never pushes the chart out of the card
    line.style.cssText = `margin-top:12px;font-size:23px;line-height:1.4;color:${CARD.text};display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;`;
    line.textContent = subtitle;
    head.appendChild(line);
  }

  return head;
}

export function render(request: RenderRequest): void {
  const host = document.getElementById('si-root');

  if (!host) throw new Error('si-root is missing');

  if (request.mode === 'card') {
    renderCard(host, request);
  } else {
    buildChartCore(host, request.data, {
      interactive: false,
      locale: request.locale,
      labels: request.labels,
      preferences: request.preferences,
      // the TradingView-style top-left meta block: title, indicator chips, OHLC/volume row
      staticInfo: true,
    });
  }

  markDone();
}

/**
 * The link-preview picture: headline over the chart, in the 1200x630 box og:image asks for.
 * The chart is the alert chart with its own title suppressed (the headline says the same
 * thing in a readable size), so the picture a shared link shows is still drawn by the very
 * chart core the app uses.
 */
function renderCard(host: HTMLElement, request: RenderRequest): void {
  const size = request.size ?? { width: 1200, height: 630, scale: 1 };

  // #si-root is the screenshot target: it must be exactly the requested box
  host.style.width = `${size.width}px`;
  host.style.height = `${size.height}px`;
  host.style.display = 'flex';
  host.style.flexDirection = 'column';
  host.style.background = CARD.background;

  // an absent field means an older API build, which always drew the brand line; null means
  // this card's headline is the brand itself
  const eyebrow = request.eyebrow === undefined ? CARD.eyebrow : request.eyebrow;

  host.appendChild(buildCardHead(request.title ?? '', request.subtitle ?? null, eyebrow));

  const chart = document.createElement('div');
  chart.style.cssText = `position:relative;flex:1 1 auto;min-height:0;margin:0 48px 36px;border:1px solid ${CARD.border};border-radius:14px;overflow:hidden;`;
  host.appendChild(chart);

  buildChartCore(chart, { ...request.data, title: '' }, {
    interactive: false,
    locale: request.locale,
    labels: request.labels,
    // The OHLC/volume read-out of the trigger candle is an alert's detail: at preview size
    // it is unreadable and it paints over the price data, so the card keeps the indicator
    // chips and the trigger marker only
    preferences: { tooltip: false, magnet: false },
    staticInfo: true,
  });
}

// lightweight-charts paints on the next animation frame; give it two so the
// screenshot never catches a half-drawn canvas
function markDone(): void {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      window.__SI_DONE__ = true;
    });
  });
}

// The built page calls this itself after the request is injected; the guard keeps the
// entry harmless when the page is opened without one (manual inspection)
if (window.__SI_REQUEST__) {
  try {
    render(window.__SI_REQUEST__);
  } catch (error) {
    window.__SI_ERROR__ = error instanceof Error ? error.message : String(error);
    window.__SI_DONE__ = true;
  }
}
