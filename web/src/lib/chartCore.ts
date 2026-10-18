import {
  AreaSeries,
  CandlestickSeries,
  ColorType,
  CrosshairMode,
  HistogramSeries,
  LineSeries,
  LineStyle,
  createChart,
  createSeriesMarkers,
  type IChartApi,
  type IPriceLine,
  type ISeriesApi,
  type ISeriesMarkersPluginApi,
  type MouseEventParams,
  type SeriesMarker,
  type Time,
  type UTCTimestamp,
} from 'lightweight-charts';
import type { ChartData } from '@/types/contracts';

// One chart implementation for every surface: the web pages, the landing previews and the
// server-side alert image (the Playwright renderer loads a page that calls this module).
// Whatever the React shell around it decides (chart preferences, fullscreen) is shared here,
// so the picture an alert shows is drawn by the same code the user sees in the app.

// Same semantic chart colors the indicator overlays use: the canvas paints them itself,
// so they are chart data colors, not UI chrome.
export const UP_COLOR = '#16a34a';
export const DOWN_COLOR = '#ef4444';
export const TRIGGER_COLOR = '#f59e0b';

// Pane proportions the user dragged: browser storage (like the theme), not the account -
// the split is a per-device view preference. Keyed by the pane layout so a two-pane chart
// never applies a three-pane split. Only interactive charts read and write it: the landing
// previews and the alert-image renderer always draw the default proportions.
const PANE_RATIOS_KEY = 'si.paneRatios';

function loadStoredPaneRatios(layout: string): number[] | null {
  try {
    const raw = localStorage.getItem(PANE_RATIOS_KEY);
    if (!raw) return null;

    const parsed: unknown = JSON.parse(raw);

    if (typeof parsed !== 'object' || parsed === null) return null;

    const value = (parsed as Record<string, unknown>)[layout];

    return Array.isArray(value) && value.length > 0 && value.every((r) => typeof r === 'number' && Number.isFinite(r) && r > 0)
      ? value as number[]
      : null;
  } catch {
    return null;
  }
}

function storePaneRatios(layout: string, ratios: number[]): void {
  try {
    let map: Record<string, unknown> = {};
    const raw = localStorage.getItem(PANE_RATIOS_KEY);

    if (raw) {
      const parsed: unknown = JSON.parse(raw);
      if (typeof parsed === 'object' && parsed !== null) map = parsed as Record<string, unknown>;
    }

    map[layout] = ratios;
    localStorage.setItem(PANE_RATIOS_KEY, JSON.stringify(map));
  } catch {
    // private mode / quota: the chart simply keeps the proportions for this session only
  }
}

// Previews never zoom, so on narrow (phone-width) containers they open already zoomed in on
// the last bars: fitContent squeezes ~120 daily candles into hairlines on a 350px chart. The
// trigger is always the last bar, so the opening window never cuts the story. Wider
// containers (desktop cards/slides) and the zoomable fullscreen chart keep fitContent.
const PREVIEW_START_BARS = 40;
const PREVIEW_NARROW_WIDTH_PX = 640;

// Price-pane vertical fit: the library defaults (top 0.2 / bottom 0.1) reserve 30% of the
// pane height, and at high zoom (a narrow price window over daily candles, plus the pixel
// reserve of the "Trigger" marker above the bar) the candles collapse into a flat sliver.
const PRICE_SCALE_MARGINS = { top: 0.1, bottom: 0.05 };

type CandlePoint = { time: UTCTimestamp; open: number; high: number; low: number; close: number; volume: number };
type VolumePoint = { time: UTCTimestamp; value: number; color: string };
type LinePoint = { time: UTCTimestamp; value: number };

/** Legend captions for the crosshair OHLC/volume row (localized by the caller) */
export interface ChartLegendLabels {
  o: string;
  h: string;
  l: string;
  c: string;
  vol: string;
}

/** The options the account's chart preferences carry (see ChartPreferencesDto) */
export interface ChartCorePreferences {
  /** OHLC/volume legend visible (hovered or last-touched candle on the web, the trigger candle on a static image) */
  tooltip: boolean;
  /** Crosshair snaps to the candle high/low/close instead of free-floating */
  magnet: boolean;
}

export interface ChartCoreOptions {
  interactive: boolean;
  locale: string;
  labels: ChartLegendLabels;
  preferences: ChartCorePreferences;
  /**
   * The alert-image layout: a TradingView-style top-left meta block (chart title, series
   * chips, and the OHLC/volume values of the trigger candle when the tooltip option is on).
   * Used only by the non-interactive server-side renderer; the web pages never set it.
   */
  staticInfo?: boolean;
  /**
   * Touch screens have no hover: the library moves the crosshair on mouse events only
   * (a finger merely pans), so on a phone the OHLC legend had no way to ever show. With
   * this flag a tap on a candle pins the crosshair and the legend to that candle until
   * another candle is tapped. Interactive surfaces only.
   */
  pinLegendOnTouch?: boolean;
  /**
   * Horizontal room (px) the shell's floating corner buttons need beyond the price
   * axis width; the OHLC legend caps its width at `calc(100% - axis - reserve)` as
   * the axis widens with zoom, so the read-out wraps above the buttons instead of
   * painting under them. Interactive surfaces with floating buttons only.
   */
  legendCornerReserve?: number;
  /** Reports the price-axis width (px) so the shell can float its corner buttons past it */
  onScaleWidth?: (width: number) => void;
}

export interface ChartCoreHandle {
  destroy(): void;
  setPreferences(preferences: ChartCorePreferences): void;
  /**
   * Applies a freshly fetched ChartData to the live chart in place: series data,
   * trigger marker, extended quote line and volumes are pushed into the existing
   * canvas with setData, so the user's zoom/pan position and the rendered frames
   * survive - unlike a destroy/rebuild, which grays the chart box out for a frame
   * (the live-update flash users read as a skeleton). Returns false when the chart
   * cannot carry the new data structurally (series set/panes changed, or this is a
   * one-shot static surface such as the alert-image renderer): the caller must
   * rebuild the chart instead.
   */
  update(data: ChartData): boolean;
}

/**
 * Structure fingerprint of a ChartData: everything that decided the chart's shape at
 * build time (series at every position with their kind/pane/style/ref-line count, the
 * volume pane). Two payloads with the same signature can be merged into one chart by
 * update(); anything else needs a rebuild.
 */
function structureSignature(data: ChartData): string {
  const series = data.series.map(
    (s) => `${s.name}|${s.kind}|${s.pane}|${s.dashed ? 1 : 0}|${s.hidden ? 1 : 0}|${(s.refLines ?? []).length}`,
  );
  const volume = data.candles.some((c) => c.volume > 0) ? 'v' : '';

  return `${volume};${series.join(';')}`;
}

/**
 * The times arrive display re-stamped (the backend shifts the candles to the user's wall
 * clock and serializes it as if UTC), so the client renders them as plain UTCTimestamps and
 * the axis reads as the user's local time.
 * lightweight-charts requires strictly ascending unique times; a DST fold can repeat a
 * wall-clock hour, so equal times collapse to the later sample.
 */
function ascending<T, R>(items: T[], time: (item: T) => number, map: (item: T) => R): R[] {
  const out: R[] = [];
  let last = -Infinity;

  for (const item of items) {
    const t = time(item);

    if (t === last && out.length > 0) {
      out[out.length - 1] = map(item);
    } else if (t > last) {
      out.push(map(item));
      last = t;
    }
  }

  return out;
}

function cssVar(name: string, fallback: string) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();

  return value.length > 0 ? value : fallback;
}

function readTheme() {
  return {
    textColor: cssVar('--color-muted-foreground', '#6b7280'),
    gridColor: cssVar('--color-border', '#e5e7eb'),
    backgroundColor: cssVar('--color-background', '#ffffff'),
    primaryColor: cssVar('--color-primary', '#3b82f6'),
  };
}

/**
 * The trigger marker. The backend payload's `trigger.direction` (computed from the handler
 * semantics) decides the arrow: up for a buy-side signal, down for a sell-side one, a
 * neutral dot for directionless events (an ADX threshold cross, a volume spike, a round
 * price hit). Only old payloads without the field fall back to client geometry: price-line
 * indicators decide it by the close crossing their line (the sign of close-line flipping
 * between the candle before the trigger and the trigger candle); comparing only the
 * position at the trigger would read a flipping line (SuperTrend jumping from support to
 * resistance on its signal candle) as the opposite signal. The tip is anchored at the
 * crossed line level (the trigger is a close-based evaluation, so the bar's high/low
 * wick extremes are not the crossing point); without a crossed line (oscillator-only
 * indicators) the marker anchors to the bar.
 */
function triggerMarker(
  data: ChartData,
  candles: CandlePoint[],
): SeriesMarker<Time> | null {
  if (!data.trigger || candles.length === 0) return null;

  const triggerTime = data.trigger.time;
  let nearest = 0;
  let best = Infinity;

  for (let i = 0; i < candles.length; i++) {
    const distance = Math.abs(candles[i].time - triggerTime);

    if (distance < best) {
      best = distance;
      nearest = i;
    }
  }

  const candle = candles[nearest];
  const pitch = candles.length > 1 ? Math.abs(candles[1].time - candles[0].time) : 60;

  const lineAt = (series: ChartData['series'][number], time: number): number | null => {
    let value: number | null = null;
    let closest = pitch;

    for (const point of series.points) {
      const distance = Math.abs(point.time - time);

      if (distance < closest) {
        closest = distance;
        value = point.value;
      }
    }

    return value;
  };

  const direction = data.trigger.direction ?? undefined;

  if (direction === 'neutral') {
    // A directionless event: a dot at the price the event happened on (the round price
    // level carried by a reference line), else just above the bar.
    let level: number | null = null;

    for (const series of data.series) {
      for (const ref of series.refLines ?? []) {
        if (ref.value >= candle.low && ref.value <= candle.high) level = ref.value;
      }
    }

    return {
      time: candle.time,
      position: 'aboveBar',
      color: TRIGGER_COLOR,
      shape: 'circle',
      text: data.trigger.label ?? '',
      ...(level !== null ? { value: level } : {}),
    };
  }

  const prevCandle = nearest > 0 ? candles[nearest - 1] : null;
  let rising: boolean | null = null;
  let crossLevel: number | null = null;
  let bestCross = Infinity;

  for (const series of data.series) {
    // hidden = deviation-band boundaries: only the indicator line itself is a crossing candidate
    if (series.pane !== 'price' || series.hidden) continue;

    const level = lineAt(series, candle.time);
    if (level === null) continue;

    const distance = Math.abs(level - candle.close);

    if (prevCandle) {
      const prevLevel = lineAt(series, prevCandle.time);

      if (prevLevel !== null) {
        const before = prevCandle.close - prevLevel;
        const after = candle.close - level;
        const crossed = before < 0 && after >= 0 ? true : before > 0 && after <= 0 ? false : null;

        if (crossed !== null && distance < bestCross) {
          bestCross = distance;
          rising = crossed;
          crossLevel = prevLevel;
        }
      }
    }
  }

  // The payload direction wins; the geometry decides only for payloads without it.
  if (direction === 'up' || direction === 'down') rising = direction === 'up';
  else if (rising === null) rising = candle.close >= candle.open;

  // Anchor the tip at the line level the close crossed (the marker's `value`), so it sits
  // on the crossing itself, not at the bar's high/low extreme; without a crossed line
  // (oscillator-only indicators) the marker keeps anchoring to the bar.
  return {
    time: candle.time,
    position: rising ? 'belowBar' : 'aboveBar',
    color: TRIGGER_COLOR,
    shape: rising ? 'arrowUp' : 'arrowDown',
    text: data.trigger.label ?? '',
    ...(crossLevel !== null ? { value: crossLevel } : {}),
  };
}

// The stamped times render "as UTC" (see ascending()), so every legend formatter pins
// timeZone UTC too - otherwise the user's own offset would shift the legend away from
// what the axis shows.
function formatters(locale: string, pitchSeconds: number) {
  const time = new Intl.DateTimeFormat(locale, pitchSeconds < 86_400
    ? { timeZone: 'UTC', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }
    : { timeZone: 'UTC', year: 'numeric', month: 'short', day: 'numeric' });
  const price = new Intl.NumberFormat(locale, { maximumFractionDigits: 6 });
  const volume = new Intl.NumberFormat(locale);

  return {
    time: (seconds: number) => time.format(seconds * 1000),
    price: (value: number) => price.format(value),
    volume: (value: number) => volume.format(value),
  };
}

interface LegendDom {
  /** The whole OHLC/volume row; display-none until the crosshair (or static info) fills it;
   * on touch screens it then pins to the last touched candle (pinLegendOnTouch) */
  row: HTMLDivElement;
  time: HTMLSpanElement;
  price: HTMLSpanElement;
  volume: HTMLSpanElement;
  volumeValue: HTMLSpanElement;
  values: [HTMLSpanElement, HTMLSpanElement, HTMLSpanElement, HTMLSpanElement];
}

function buildLegend(host: HTMLElement, labels: ChartLegendLabels, theme: ReturnType<typeof readTheme>): LegendDom {
  const row = document.createElement('div');
  // pointer-events none: every gesture must reach the chart below the legend; hidden until
  // the crosshair (or the static alert image) fills it
  row.style.cssText = `position:absolute;left:8px;top:8px;z-index:10;pointer-events:none;display:none;flex-wrap:wrap;align-items:center;column-gap:12px;row-gap:4px;max-width:calc(100% - 4rem);font-size:12px;line-height:1.4;border-radius:6px;padding:6px 8px;background:color-mix(in srgb, ${theme.backgroundColor} 85%, transparent);border:1px solid color-mix(in srgb, ${theme.gridColor} 60%, transparent);`;

  const time = document.createElement('span');
  time.style.color = theme.textColor;
  row.appendChild(time);

  const mkGroup = (): HTMLSpanElement => {
    const group = document.createElement('span');
    group.style.cssText = 'display:flex;flex-wrap:wrap;align-items:center;column-gap:10px;row-gap:2px;border-radius:4px;margin:0 -4px;padding:0 4px;';

    return group;
  };

  const price = mkGroup();
  const values: HTMLSpanElement[] = [];

  for (const caption of [labels.o, labels.h, labels.l, labels.c]) {
    const cell = document.createElement('span');
    cell.style.cssText = 'display:flex;align-items:center;gap:4px;';
    const label = document.createElement('span');
    label.style.color = theme.textColor;
    label.textContent = caption;
    const value = document.createElement('span');
    value.style.fontFamily = 'ui-monospace,monospace';
    cell.appendChild(label);
    cell.appendChild(value);
    price.appendChild(cell);
    values.push(value);
  }

  row.appendChild(price);

  const volume = mkGroup();
  const volumeLabel = document.createElement('span');
  volumeLabel.style.color = theme.textColor;
  volumeLabel.textContent = labels.vol;
  const volumeValue = document.createElement('span');
  volumeValue.style.fontFamily = 'ui-monospace,monospace';
  volume.appendChild(volumeLabel);
  volume.appendChild(volumeValue);
  row.appendChild(volume);

  host.appendChild(row);

  return { row, time, price, volume, volumeValue, values: values as LegendDom['values'] };
}

/** TradingView-style meta block of the static alert image: title + indicator chips */
function buildStaticInfo(host: HTMLElement, data: ChartData, theme: ReturnType<typeof readTheme>): HTMLElement {
  const block = document.createElement('div');
  block.style.cssText = `position:absolute;left:8px;top:8px;z-index:10;pointer-events:none;display:flex;flex-direction:column;gap:3px;max-width:calc(100% - 4rem);`;
  host.appendChild(block);

  // A surface that has nothing to say in the title (the link-preview card names the
  // indicator in its own headline above) must not reserve an empty line for it
  if (data.title) {
    const title = document.createElement('div');
    title.style.cssText = `font-size:13px;font-weight:600;font-family:ui-monospace,monospace;`;
    title.textContent = data.title;
    block.appendChild(title);
  }

  const chips = document.createElement('div');
  chips.style.cssText = `display:flex;flex-wrap:wrap;column-gap:10px;row-gap:2px;font-size:11px;color:${theme.textColor};`;

  for (const series of data.series) {
    if (series.hidden) continue;
    const chip = document.createElement('span');
    chip.style.cssText = `display:inline-flex;align-items:center;gap:4px;`;
    const swatch = document.createElement('span');
    swatch.style.cssText = `width:8px;height:8px;border-radius:2px;background:${series.color};`;
    chip.appendChild(swatch);
    chip.appendChild(document.createTextNode(series.name));
    chips.appendChild(chip);
  }

  block.appendChild(chips);

  return block;
}

/**
 * Builds a lightweight-charts candlestick chart (candles + indicator overlays + trigger
 * marker + volume pane + OHLC/volume legend) into `host` (a positioned, sized container).
 * The host is left untouched apart from the DOM the chart and its legend add; destroy()
 * removes everything this function created.
 */
export function buildChartCore(host: HTMLElement, data: ChartData, options: ChartCoreOptions): ChartCoreHandle {
  const { interactive, locale, labels, staticInfo = false, pinLegendOnTouch = false } = options;
  let prefs = options.preferences;
  const theme = readTheme();

  const chartDiv = document.createElement('div');
  chartDiv.style.cssText = 'height:100%;width:100%;';
  host.appendChild(chartDiv);

  const fmt = formatters(locale, data.candles.length > 1 ? data.candles[1].time - data.candles[0].time : 86_400);

  const legend = buildLegend(host, labels, theme);
  // The static info block (title + chips) flows above the legend row in one top-left column
  let infoBlock: HTMLElement | null = null;

  if (staticInfo) {
    infoBlock = buildStaticInfo(host, data, theme);
    infoBlock.appendChild(legend.row);
    legend.row.style.position = 'static';
    legend.row.style.background = 'transparent';
    legend.row.style.border = 'none';
    legend.row.style.padding = '0';
  }

  const chart: IChartApi = createChart(chartDiv, {
    // A small right offset mirrors the margin: the last candle is not glued to the edge
    width: host.clientWidth,
    height: host.clientHeight,
    layout: {
      background: { type: ColorType.Solid, color: 'rgba(0, 0, 0, 0)' },
      textColor: theme.textColor,
      fontSize: 11,
      panes: { separatorColor: theme.gridColor, separatorHoverColor: theme.gridColor },
      // the logo is a focusable <a> inside the chart host, which is aria-hidden
      // (canvas is unreadable in the a11y tree) - a focusable node there breaks axe's
      // aria-hidden-focus. The attribution link requirement is met by /legal/licenses
      // (footer link on every page), so the on-canvas logo is optional
      attributionLogo: false,
    },
    grid: { vertLines: { color: theme.gridColor, style: LineStyle.Dotted }, horzLines: { color: theme.gridColor, style: LineStyle.Dotted } },
    rightPriceScale: { borderColor: theme.gridColor },
    timeScale: { borderColor: theme.gridColor, timeVisible: true, secondsVisible: false, rightOffset: 4 },
    crosshair: { mode: interactive ? (prefs.magnet ? CrosshairMode.Magnet : CrosshairMode.Normal) : CrosshairMode.Hidden },
    // previews (landing gallery) never pan by drag and never zoom by wheel/pinch: every
    // gesture belongs to the page (document scroll / carousel slide), panning and zooming
    // live in the fullscreen dialog; the library only consumes touches when a drag flag is
    // on, otherwise it never preventDefaults and the swipe passes through
    handleScroll: interactive,
    handleScale: interactive,
    autoSize: false,
    // the time axis is drawn by the library through Intl; by default it uses
    // navigator.language, so a Russian browser would label the axis in Russian even when
    // the UI language is switched to English - pin it to the app locale like every other
    // date in the app (the crosshair legend formats with `locale` too)
    localization: { locale },
  });

  let candles = ascending(data.candles, (c) => c.time, (c): CandlePoint => ({
    time: c.time as UTCTimestamp,
    open: c.open,
    high: c.high,
    low: c.low,
    close: c.close,
    volume: c.volume,
  }));

  const candleSeries = chart.addSeries(CandlestickSeries, {
    upColor: UP_COLOR,
    downColor: DOWN_COLOR,
    wickUpColor: UP_COLOR,
    wickDownColor: DOWN_COLOR,
    borderVisible: false,
  }, 0);
  // the price pane shares the 'right' scale across candles and indicator overlays
  chart.priceScale('right').applyOptions({ scaleMargins: PRICE_SCALE_MARGINS });
  candleSeries.setData(candles);

  // The live-update state: everything a structural payload can carry that the update()
  // must push into the existing chart instead of rebuilding. The signature freezes the
  // shape of the payload this chart was built for; any drift means a rebuild.
  let structure = structureSignature(data);
  let markersPlugin: ISeriesMarkersPluginApi<Time> | null = null;

  const initialMarker = triggerMarker(data, candles);

  if (initialMarker) markersPlugin = createSeriesMarkers(candleSeries, [initialMarker]);

  // The extended-hours quote rides across the chart as a purple dashed line labeled with
  // the session name (the way TradingView tags the overnight/pre-market last trade).
  // The merged live candle already ends at this price, so the candle's own last-value tag
  // shows the number; the line carries the localized session label (same source as the
  // trigger marker's label). No axis label: it would double-tag the very same price.
  let extendedLine: IPriceLine | null = null;

  if (data.extended && data.extended.price > 0) {
    extendedLine = candleSeries.createPriceLine({
      price: data.extended.price,
      color: '#a855f7',
      lineWidth: 1,
      lineStyle: LineStyle.Dashed,
      axisLabelVisible: false,
      title: data.extended.label,
    });
  }

  const hasOscillator = data.series.some((s) => s.pane === 'oscillator');
  const hasVolume = data.candles.some((c) => c.volume > 0);
  // the pane the volume histogram lives in (the legend highlights the Vol group when the
  // crosshair is over it); undefined when there is no volume pane
  const volumePane = hasVolume ? (hasOscillator ? 2 : 1) : undefined;

  // index-aligned with data.series (the signature pins positions, so an update maps
  // series by index and never has to match them by name)
  const liveSeries: { api: ISeriesApi<'Area' | 'Histogram' | 'Line'>; priceLines: IPriceLine[] }[] = [];

  data.series.forEach((series) => {
    const pane = series.pane === 'oscillator' && hasOscillator ? 1 : 0;
    const points = ascending(series.points, (p) => p.time, (p): LinePoint => ({
      time: p.time as UTCTimestamp,
      value: p.value,
    }));

    // Hidden series (deviation-band boundaries) annotate the signal line, so they stay out of
    // the legend chips, and out of the price autoscale too: a wide tolerance band (a 1%
    // deviation on minutes spans several times the price movement of the window) would make
    // fitContent fit the pane to the band and crush the candles into sub-pixel dashes. The
    // last-value axis label goes with it: once a band sits beyond the fitted range, the library
    // pins its label to the pane edge, which reads as a broken axis.
    const seriesOptions = {
      color: series.color,
      title: series.hidden ? '' : series.name,
      priceLineVisible: false,
      lastValueVisible: series.pane === 'price' && !series.hidden,
      autoscaleInfoProvider: series.hidden ? () => null : undefined,
    };
    // "bar" is the backend's histogram kind (MACD histogram, oscillator Volume): thin bars
    // from zero, exactly what the {time, value} points carry
    let api: ISeriesApi<'Area' | 'Histogram' | 'Line'>;

    if (series.kind === 'area') {
      api = chart.addSeries(AreaSeries, seriesOptions, pane);
    } else if (series.kind === 'bar') {
      api = chart.addSeries(HistogramSeries, seriesOptions, pane);
    } else if (series.dashed) {
      // thin dashed stroke for deviation-band boundaries
      api = chart.addSeries(LineSeries, { ...seriesOptions, lineWidth: 1, lineStyle: LineStyle.Dashed }, pane);
    } else {
      api = chart.addSeries(LineSeries, { ...seriesOptions, lineWidth: 2 }, pane);
    }

    api.setData(points);

    const priceLines = series.refLines.map((refLine) => api.createPriceLine({
      price: refLine.value,
      color: refLine.color,
      lineWidth: 1,
      lineStyle: LineStyle.Dashed,
      axisLabelVisible: true,
      title: refLine.label ?? '',
    }));

    liveSeries.push({ api, priceLines });
  });

  let volumeSeries: ISeriesApi<'Histogram'> | null = null;

  if (hasVolume && volumePane !== undefined) {
    volumeSeries = chart.addSeries(HistogramSeries, {
      priceFormat: { type: 'volume' },
      priceLineVisible: false,
      lastValueVisible: false,
    }, volumePane);
    volumeSeries.setData(ascending(data.candles, (c) => c.time, (c): VolumePoint => ({
      time: c.time as UTCTimestamp,
      value: c.volume,
      color: c.close >= c.open ? 'rgba(22, 163, 74, 0.45)' : 'rgba(239, 68, 68, 0.45)',
    })));
  }

  // Initial pane proportions: the indicator pane is the one the user came to read, so it
  // takes 30% of the chart, while the volume histogram stays a compact strip; the candles
  // take the rest. These are stretch factors, not setHeight pixels - the library's layout
  // pass recomputes every pane from the factors (setHeight values are silently overwritten
  // by the next resize/fit), while factors persist across resizes. Once the user drags a
  // separator, the resulting proportions are remembered in browser storage and restored
  // for every chart with the same pane layout.
  const defaultRatios = hasOscillator && hasVolume
    ? [0.59, 0.30, 0.11]
    : hasOscillator
      ? [0.70, 0.30]
      : hasVolume
        ? [0.89, 0.11]
        : [1];
  const paneLayout = `${hasOscillator ? 'o' : ''}${hasVolume ? 'v' : ''}`;
  const storedRatios = interactive && defaultRatios.length > 1 ? loadStoredPaneRatios(paneLayout) : null;
  const paneRatios = storedRatios && storedRatios.length === defaultRatios.length ? storedRatios : defaultRatios;

  chart.panes().forEach((pane, index) => pane.setStretchFactor(paneRatios[index]));

  // The separator drag lives entirely inside the library (no public resize event in v5),
  // so the release of any pointer is the moment to compare the live factors with the last
  // known ones and persist only an actual change (the initial factors count as known).
  let knownRatios = paneRatios.map((ratio) => Math.round(ratio * 1e6) / 1e6);

  const persistPaneRatios = () => {
    const current = chart.panes().map((pane) => Math.round(pane.getStretchFactor() * 1e6) / 1e6);

    if (current.length !== knownRatios.length || current.every((ratio, index) => Math.abs(ratio - knownRatios[index]) < 1e-9)) {
      return;
    }

    knownRatios = current;
    storePaneRatios(paneLayout, current);
  };

  if (interactive && defaultRatios.length > 1) {
    window.addEventListener('pointerup', persistPaneRatios);
  }

  chart.timeScale().fitContent();

  // width() is a method: passing it uninvited would typecheck as a SetStateAction
  // updater, and React would invoke it with this===undefined (the library crashes then)
  const syncScaleWidth = () => {
    const axisWidth = chart.priceScale('right').width();

    // the corner buttons float at `right: axis + offset`, so the legend's right cap
    // must track the axis too, or a zoom into wide prices slides it back under them
    if (interactive && options.legendCornerReserve !== undefined) {
      legend.row.style.maxWidth = `calc(100% - ${axisWidth + options.legendCornerReserve}px)`;
    }

    options.onScaleWidth?.(axisWidth);
  };

  // The price axis widens/shrinks as the zoomed range shows prices of different widths,
  // and neither resize nor re-create fires then - the range subscription does
  chart.timeScale().subscribeVisibleLogicalRangeChange(syncScaleWidth);
  syncScaleWidth();

  // Previews never zoom, so on narrow (phone-width) screens they open already zoomed in
  // on the last bars around the trigger; fitContent would squeeze ~120 daily candles into
  // hairlines. The fullscreen dialog (interactive) and the alert image (wide) start at
  // fitContent and zoom themselves.
  if (!interactive && candles.length > PREVIEW_START_BARS && host.clientWidth < PREVIEW_NARROW_WIDTH_PX) {
    chart.timeScale().setVisibleLogicalRange({
      from: candles.length - PREVIEW_START_BARS,
      to: candles.length + 2,
    });
  }

  // Trackpad pinch reaches the DOM as ctrl+wheel with sub-unit deltaY deltas, while the
  // library's wheel-zoom step is sign*min(1,|delta|/100) per event: a pinch crawls and only
  // a mouse wheel (delta 100+) or a touch pinch (its own x5 multiplier) feels fast. Take
  // ctrl+wheel over on the capture phase (the library never sees it) and zoom
  // multiplicatively around the cursor, so a pinch scales like on touch. Interactive only:
  // previews must leave the wheel to the page.
  const onPinchWheel = (event: WheelEvent) => {
    if (!event.ctrlKey) return;
    const timeScale = chart.timeScale();
    const range = timeScale.getVisibleLogicalRange();

    if (!range) return;
    event.preventDefault();
    event.stopPropagation();

    const rect = host.getBoundingClientRect();
    const span = range.to - range.from;
    const anchor = range.from + (span * (event.clientX - rect.left)) / Math.max(1, rect.width);
    // exp keeps pinch-in/out symmetric; the per-event clamp keeps a notched mouse
    // wheel (delta 100) from spanning the whole series in one tick
    const factor = Math.min(2, Math.max(0.5, Math.exp(event.deltaY * 0.05)));
    timeScale.setVisibleLogicalRange({
      from: anchor - (anchor - range.from) * factor,
      to: anchor + (range.to - anchor) * factor,
    });
  };

  if (interactive) {
    host.addEventListener('wheel', onPinchWheel, { capture: true, passive: false });
  }

  const highlight = `color-mix(in srgb, ${theme.primaryColor} 10%, transparent)`;

  const paintLegend = (candle: CandlePoint, focus: 'price' | 'volume' | null) => {
    legend.time.textContent = fmt.time(candle.time);
    const up = candle.close >= candle.open;
    const colors = [up ? UP_COLOR : DOWN_COLOR, UP_COLOR, DOWN_COLOR, up ? UP_COLOR : DOWN_COLOR];
    const values = [candle.open, candle.high, candle.low, candle.close];
    legend.values.forEach((span, i) => {
      span.textContent = fmt.price(values[i]);
      span.style.color = colors[i];
    });
    const hasVolume = candle.volume > 0;
    legend.volumeValue.textContent = fmt.volume(candle.volume);
    // the group the crosshair sits over reads highlighted (price pane vs volume pane)
    legend.price.style.background = focus === 'price' ? highlight : '';
    legend.volume.style.background = focus === 'volume' ? highlight : '';
    legend.volume.style.display = hasVolume ? 'flex' : 'none';
  };

  const candleByTime = new Map<number, CandlePoint>();
  const syncCandleIndex = () => {
    candleByTime.clear();
    for (const c of candles) candleByTime.set(c.time, c);
  };
  syncCandleIndex();

  // the candle the touch-pinned legend currently shows (see pinLegendOnTouch)
  let pinnedCandle: CandlePoint | null = null;

  const onCrosshair = (param: MouseEventParams) => {
    const candle = param.time === undefined ? undefined : candleByTime.get(param.time as number);

    if (!candle) {
      // A clear fired by a hybrid pointer (mouse leaving the chart) must not wipe a
      // tap-pinned read-out: on touch surfaces the last touched candle stays on screen
      if (pinnedCandle && pinLegendOnTouch && prefs.tooltip) {
        paintLegend(pinnedCandle, null);
        legend.row.style.display = 'flex';
      } else {
        legend.row.style.display = 'none';
      }

      return;
    }

    pinnedCandle = candle;
    paintLegend(candle, param.paneIndex === volumePane ? 'volume' : param.paneIndex === 0 ? 'price' : null);

    if (prefs.tooltip) legend.row.style.display = 'flex';
  };

  // The tap that replaces hovering on touch screens. The library never raises the
  // crosshair for a touch itself (fingers only pan), so on a phone the tooltip option
  // used to be dead: a tap now pins the crosshair to the tapped candle (its close
  // carries the horizontal line; the magnet aligns a synthetic position anyway), and
  // the OHLC read-out stays until another candle is tapped
  const onTap = (param: MouseEventParams) => {
    if (!pinLegendOnTouch || !prefs.tooltip) return;
    const candle = param.time === undefined ? undefined : candleByTime.get(param.time as number);

    if (!candle) return;

    pinnedCandle = candle;
    chart.setCrosshairPosition(candle.close, candle.time, candleSeries);
    paintLegend(candle, param.paneIndex === volumePane ? 'volume' : param.paneIndex === 0 ? 'price' : null);
    legend.row.style.display = 'flex';
  };

  if (interactive) {
    chart.subscribeCrosshairMove(onCrosshair);

    if (pinLegendOnTouch) chart.subscribeClick(onTap);
  } else if (staticInfo) {
    // The alert image pins the trigger candle (always the last one on a trigger chart) so
    // the PNG carries the same OHLC/volume read-out the tooltip on the web would show
    const triggerCandle = data.trigger
      ? candleByTime.get(data.trigger.time) ?? candles[candles.length - 1]
      : candles[candles.length - 1];

    if (triggerCandle) {
      paintLegend(triggerCandle, null);
      legend.row.style.display = prefs.tooltip ? 'flex' : 'none';
    }
  }

  const observer = new ResizeObserver(() => {
    // pane stretch factors persist across resizes (the layout pass re-derives heights
    // from them), so a separator the user dragged is deliberately not reset here
    chart.applyOptions({ width: host.clientWidth, height: host.clientHeight });
    syncScaleWidth();
  });
  observer.observe(host);

  return {
    destroy() {
      observer.disconnect();
      chart.timeScale().unsubscribeVisibleLogicalRangeChange(syncScaleWidth);

      if (interactive) {
        host.removeEventListener('wheel', onPinchWheel, { capture: true });
        window.removeEventListener('pointerup', persistPaneRatios);
        chart.unsubscribeCrosshairMove(onCrosshair);

        if (pinLegendOnTouch) chart.unsubscribeClick(onTap);
      }
      chart.remove();
      chartDiv.remove();
      legend.row.remove();
      infoBlock?.remove();
    },
    setPreferences(next: ChartCorePreferences) {
      prefs = next;

      if (interactive) {
        chart.applyOptions({ crosshair: { mode: next.magnet ? CrosshairMode.Magnet : CrosshairMode.Normal } });

        if (!next.tooltip) {
          legend.row.style.display = 'none';
          if (pinLegendOnTouch) chart.clearCrosshairPosition();
        } else if (pinLegendOnTouch && pinnedCandle) {
          // the option came back on: the candle the finger last touched is still the read-out
          chart.setCrosshairPosition(pinnedCandle.close, pinnedCandle.time, candleSeries);
          paintLegend(pinnedCandle, null);
          legend.row.style.display = 'flex';
        }
      } else if (staticInfo) {
        legend.row.style.display = next.tooltip ? 'flex' : 'none';
      }
    },
    update(next: ChartData): boolean {
      // one-shot static surfaces (the alert-image renderer, landing previews) draw once
      if (!interactive) return false;

      const nextStructure = structureSignature(next);

      if (nextStructure !== structure) return false;

      const previousLength = candles.length;
      // a chart parked at the right edge follows the new bars; one scrolled back stays put
      const visibleBefore = chart.timeScale().getVisibleLogicalRange();

      candles = ascending(next.candles, (c) => c.time, (c): CandlePoint => ({
        time: c.time as UTCTimestamp,
        open: c.open,
        high: c.high,
        low: c.low,
        close: c.close,
        volume: c.volume,
      }));
      candleSeries.setData(candles);
      syncCandleIndex();

      // a touch-pinned read-out follows the candle's fresh values (the live last bar keeps
      // moving after the pin); the stale object is dropped once its time leaves the series
      if (pinnedCandle) {
        const fresh = candleByTime.get(pinnedCandle.time);

        if (fresh) {
          pinnedCandle = fresh;

          if (pinLegendOnTouch && prefs.tooltip && legend.row.style.display === 'flex') {
            paintLegend(fresh, null);
            chart.setCrosshairPosition(fresh.close, fresh.time, candleSeries);
          }
        } else {
          pinnedCandle = null;
        }
      }

      const marker = triggerMarker(next, candles);

      if (markersPlugin) markersPlugin.setMarkers(marker ? [marker] : []);
      else if (marker) markersPlugin = createSeriesMarkers(candleSeries, [marker]);

      if (next.extended && next.extended.price > 0) {
        if (extendedLine) extendedLine.applyOptions({ price: next.extended.price, title: next.extended.label });
        else {
          extendedLine = candleSeries.createPriceLine({
            price: next.extended.price,
            color: '#a855f7',
            lineWidth: 1,
            lineStyle: LineStyle.Dashed,
            axisLabelVisible: false,
            title: next.extended.label,
          });
        }
      } else if (extendedLine) {
        candleSeries.removePriceLine(extendedLine);
        extendedLine = null;
      }

      next.series.forEach((series, index) => {
        const entry = liveSeries[index];
        entry.api.setData(ascending(series.points, (p) => p.time, (p): LinePoint => ({
          time: p.time as UTCTimestamp,
          value: p.value,
        })));
        // the signature keeps the ref-line counts per position: push the (recomputed)
        // indicator levels into the existing lines rather than churn them
        series.refLines.forEach((refLine, lineIndex) => {
          entry.priceLines[lineIndex].applyOptions({
            price: refLine.value,
            color: refLine.color,
            title: refLine.label ?? '',
          });
        });
      });

      if (volumeSeries) {
        volumeSeries.setData(ascending(next.candles, (c) => c.time, (c): VolumePoint => ({
          time: c.time as UTCTimestamp,
          value: c.volume,
          color: c.close >= c.open ? 'rgba(22, 163, 74, 0.45)' : 'rgba(239, 68, 68, 0.45)',
        })));
      }

      const delta = candles.length - previousLength;

      // strictly past the last bar: the user sits in the right-offset zone (following the
      // live edge); a fitContent view ends exactly on the last bar and must not slide
      if (delta !== 0 && visibleBefore && visibleBefore.to > previousLength - 1) {
        chart.timeScale().setVisibleLogicalRange({
          from: visibleBefore.from + delta,
          to: visibleBefore.to + delta,
        });
      }

      return true;
    },
  };
}
