import { useEffect, useRef, useState } from 'react';
import { Maximize2 } from 'lucide-react';
import type { ChartData } from '@/types/contracts';
import { buildChartCore, type ChartCoreHandle } from '@/lib/chartCore';
import { useTranslation } from '@/i18n';
import { useHasHover } from '@/hooks/useHasHover';
import { useAuth } from '@/hooks/useAuth';
import { useChartPreferences } from '@/hooks/useChartPreferences';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { ChartSettingsButton } from '@/components/ChartSettingsButton';
import { Dialog, DialogContent } from '@/components/ui/dialog';

/**
 * The web shell around the shared chart core (lib/chartCore): candles + indicator overlays
 * + trigger marker + volume pane, drawn by the same module the server-side alert image
 * renders, so the picture in an alert is the picture on the page. This component adds the
 * per-account chart preferences (persisted like the subscriptions sort), the crosshair
 * tooltip the tooltip option toggles, and the fullscreen viewer.
 * `interactive: false` renders a static preview (landing gallery): no drag pans it and no
 * wheel/pinch zooms it, so every mouse/touch gesture over a chart goes to the page (a
 * vertical swipe scrolls the document, a horizontal one moves the carousel, the wheel
 * scrolls the page); panning and zooming live in the fullscreen dialog.
 * `hint: false` drops the zoom hint line. `fill` makes the chart stretch to its flex
 * parent's height (fullscreen dialog) instead of holding the fixed 2:1 aspect.
 */
export function InteractiveChart({ data, hint = true, interactive = true, fill = false }: { data: ChartData; hint?: boolean; interactive?: boolean; fill?: boolean }) {
  const hostRef = useRef<HTMLDivElement>(null);
  const coreRef = useRef<ChartCoreHandle | null>(null);
  const { t, locale } = useTranslation();
  const hasHover = useHasHover();

  // Recreating the chart when the theme flips keeps the canvas text/grid in the active theme
  // (the canvas cannot follow CSS variables on its own)
  const [themeTick, setThemeTick] = useState(0);

  // Live ticks push new data into the existing chart (core.update): destroy/rebuild on
  // every chartUpdate grays the canvas for a frame and resets zoom/pan, which users read
  // as a skeleton flash. A structural change the core cannot carry bumps this tick and
  // goes through the full rebuild. dataRef lets the rebuild build the freshest payload
  // without chasing data in the build effect's deps.
  const [rebuildTick, setRebuildTick] = useState(0);
  const dataRef = useRef(data);
  dataRef.current = data;
  const appliedRef = useRef<ChartData | null>(null);

  // Width of the right price axis (px): the corner buttons are offset by it so they float
  // over the plot, not over the axis labels they used to cover; synced by the core after
  // every layout and visible-range change
  const [scaleWidth, setScaleWidth] = useState(0);

  // Fullscreen viewer: the same data re-rendered by a nested interactive chart that fills
  // the viewport (the dialog chart itself has no button - fill - so this never recurses)
  const [fullscreen, setFullscreen] = useState(false);

  // Chart display options (per account, persisted via PATCH /account/preferences; the
  // shared store in useChartPreferences keeps the gear - which in fullscreen lives in
  // the dialog's title row, not in this instance - and this chart's canvas in sync)
  const { isAuthenticated } = useAuth();
  const { tooltipEnabled, magnetEnabled } = useChartPreferences();

  const legend = t.subscriptions.triggerHistory.chart.legend;

  useEffect(() => {
    const observer = new MutationObserver(() => setThemeTick((tick) => tick + 1));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const host = hostRef.current;

    if (!host) return;

    // The core owns the canvas and the hover legend (pinned to the tapped candle on
    // touch); the legend captions localize with the locale, which rebuilds the chart
    // (the library-drawn time axis follows it too)
    const payload = dataRef.current;
    const core = buildChartCore(host, payload, {
      interactive,
      locale,
      labels: legend,
      preferences: { tooltip: tooltipEnabled, magnet: magnetEnabled },
      // touch screens have no cursor to hover with: a tap on a candle pins the OHLC row
      pinLegendOnTouch: !hasHover,
      // the corner buttons float over the chart only here (fullscreen moved its gear to
      // the dialog title row): the legend wraps past them instead of under them
      legendCornerReserve: !fill ? 112 : undefined,
      onScaleWidth: setScaleWidth,
    });
    coreRef.current = core;
    appliedRef.current = payload;

    return () => {
      core.destroy();
      coreRef.current = null;
    };
    // themeTick re-creates the chart on a theme flip so the canvas picks up the new CSS
    // colors; legend is locale-derived and only changes with it; rebuildTick is the core's
    // own fallback when new data no longer fits the built structure; hasHover is read at
    // build time (no live channel), so an input change (e.g. a mouse on a tablet) rebuilds.
    // Live data changes deliberately do NOT rebuild (see the update effect below).
  }, [rebuildTick, themeTick, interactive, locale, legend, hasHover, fill]);

  // Live ticks: hand the fresh payload to the running chart. Anything the canvas cannot
  // absorb in place (indicator series came and went, volume appeared) rebuilds it once.
  useEffect(() => {
    if (coreRef.current === null || appliedRef.current === data) return;

    if (!coreRef.current.update(data)) setRebuildTick((tick) => tick + 1);
  }, [data]);

  // Preference flips without a chart rebuild: the magnet goes straight to the crosshair
  // options, the tooltip option hides the hover legend
  useEffect(() => {
    coreRef.current?.setPreferences({ tooltip: tooltipEnabled, magnet: magnetEnabled });
  }, [tooltipEnabled, magnetEnabled]);

  return (
    <div className={cn('space-y-2', fill && 'flex h-full min-h-0 flex-col')}>
      {/* Interactive (dashboard) keeps the 2:1 frame of the alert image so switching
          surfaces does not reflow the page. Static previews (landing) are taller - portrait
          3:4 on a phone and 4:3 on desktop, where 2:1 is a short letterbox over the candles;
          fill (fullscreen dialog) stretches to the parent's height. */}
      <div className={cn(
        'relative w-full overflow-hidden rounded-md border border-border bg-muted/20',
        fill ? 'min-h-0 flex-1' : interactive ? 'aspect-[2/1]' : 'aspect-[3/4] md:aspect-[4/3]',
      )}>
        {/* The library paints its layout tables inside the chart host: hidden from the
            accessibility tree (canvas charts are unreadable there anyway), while the
            overlay buttons below stay reachable */}
        <div ref={hostRef} aria-hidden="true" className="absolute inset-0" />
        {/* Chart display options (tooltip, crosshair magnet): floating over a dashboard
            card's corner; in the fullscreen dialog the same gear lives in the dialog's
            title row instead, above the chart, so it never covers the OHLC legend.
            Signed-in only: anonymous landing previews have nothing to persist */}
        {interactive && isAuthenticated && !fill && (
          <ChartSettingsButton
            className="absolute top-1.5 z-10 size-11 bg-background/70 backdrop-blur-sm"
            style={{ right: scaleWidth + 56 }}
          />
        )}

        {/* Fullscreen viewer (the dashboard charts are small boxes; the landing dialog chart
            is already fullscreen - fill - so it gets no button) */}
        {interactive && !fill && (
          <Button
            variant="ghost"
            size="icon"
            className="absolute top-1.5 z-10 size-11 bg-background/70 backdrop-blur-sm"
            style={{ right: scaleWidth + 6 }}
            aria-label={t.landing.expand}
            onClick={() => setFullscreen(true)}
          >
            <Maximize2 className="h-5 w-5" />
          </Button>
        )}
      </div>

      {hint && (
        <p className="text-center text-xs text-muted-foreground">
          {hasHover
            ? t.subscriptions.triggerHistory.chart.zoomHintDesktop
            : t.subscriptions.triggerHistory.chart.zoomHint}
        </p>
      )}

      {interactive && (
        <Dialog open={fullscreen} onOpenChange={setFullscreen}>
          <DialogContent size="fullscreen">
            <div className="flex h-full min-h-0 flex-col gap-3">
              {/* 44px-tall toolbar line: the gear joins the title, the X aligns on its center;
                  pr-11 sm:pr-9 = card padding + gear-to-X gap of exactly one gap-2 */}
              <div className="flex min-h-11 flex-wrap items-center gap-2 pr-11 sm:pr-9">
                <span className="font-mono text-lg font-semibold">{data.title}</span>
                {isAuthenticated && <ChartSettingsButton className="ml-auto size-11" />}
              </div>
              <div className="min-h-0 flex-1">
                <InteractiveChart data={data} fill interactive hint={false} />
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}
