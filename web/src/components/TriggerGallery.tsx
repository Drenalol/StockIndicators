import { useCallback, useEffect, useRef, useState } from 'react';
import { useReducedMotion } from 'motion/react';
import { ChevronLeft, ChevronRight, Maximize2 } from 'lucide-react';
import type { LandingExampleChartDto } from '@/types/contracts';
import { useTranslation } from '@/i18n';
import { useHasHover } from '@/hooks/useHasHover';
import { getIndicatorShortName } from '@/i18n/indicatorTranslations';
import { ExchangeLogo } from '@/components/ExchangeLogo';
import { InteractiveChart } from '@/components/InteractiveChart';
import { ChartDialog } from '@/components/ChartDialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

const AUTO_ADVANCE_MS = 5000;

// How long after the last user touch the auto-advance resumes ("auto-scrolls
// when you don't touch it"): a pause this short reads as broken, longer hides it
const RESUME_AFTER_MS = 8000;

// Left-button movement (px) before a mousedown becomes a track drag, so a click on
// the arrows/dots/expand button is never mistaken for a drag
const DRAG_THRESHOLD_PX = 6;

/**
 * Auto-advancing chart carousel for the landing page: real trigger-shaped example
 * charts from the server (see GET /api/landing/examples). Slide changes go through the
 * arrows (plus dots on desktop), the auto-advance, horizontal swipes over the track and -
 * on hover devices - dragging the track with the mouse (charts are static previews and
 * never eat the gesture). It pauses while hovered and right after a drag/wheel, then
 * resumes on its own.
 */
export function TriggerGallery({ examples }: { examples: LandingExampleChartDto[] }) {
  const { t } = useTranslation();
  const reducedMotion = useReducedMotion();
  const hasHover = useHasHover();
  const trackRef = useRef<HTMLDivElement>(null);
  const activeRef = useRef(0);
  const lastInteractRef = useRef(0);
  const hoveredRef = useRef(false);
  const [active, setActive] = useState(0);
  const [expanded, setExpanded] = useState<LandingExampleChartDto | null>(null);
  // the fullscreen dialog covers the track; auto-advance would move a slide behind it
  const expandedRef = useRef(false);
  expandedRef.current = expanded !== null;
  const [dragging, setDragging] = useState(false);

  const count = examples.length;
  const g = t.landing.gallery;

  const scrollToIndex = useCallback((index: number, instant = false) => {
    const track = trackRef.current;
    const slide = track?.children[index] as HTMLElement | undefined;

    if (!track || !slide) return;
    track.scrollTo({ left: slide.offsetLeft, behavior: instant || reducedMotion ? 'auto' : 'smooth' });
    activeRef.current = index;
    setActive(index);
  }, [reducedMotion]);

  useEffect(() => {
    if (reducedMotion || count < 2) return;
    const timer = window.setInterval(() => {
      if (document.hidden || hoveredRef.current || expandedRef.current) return;
      if (Date.now() - lastInteractRef.current < RESUME_AFTER_MS) return;
      // wrap: jump back without rewinding through every slide
      const next = (activeRef.current + 1) % count;
      scrollToIndex(next, next === 0);
    }, AUTO_ADVANCE_MS);

    return () => window.clearInterval(timer);
  }, [count, reducedMotion, scrollToIndex]);

  const markUserInteraction = useCallback(() => {
    lastInteractRef.current = Date.now();
  }, []);

  // Index of the slide whose snap position is nearest the current scroll offset
  const nearestIndex = useCallback(() => {
    const track = trackRef.current;

    if (!track) return 0;
    let nearest = 0;
    let best = Infinity;

    for (let i = 0; i < track.children.length; i++) {
      const distance = Math.abs((track.children[i] as HTMLElement).offsetLeft - track.scrollLeft);

      if (distance < best) {
        best = distance;
        nearest = i;
      }
    }

    return nearest;
  }, []);

  // The snap settles the scroll at a slide; track it so dots and the next
  // auto-advance step follow manual drags too (fires on programmatic scrolls as
  // well - same result, the nearest slide is the one we scrolled to)
  const handleScroll = useCallback(() => {
    const nearest = nearestIndex();
    activeRef.current = nearest;
    setActive(nearest);
  }, [nearestIndex]);

  // Desktop: dragging the track with the mouse pans the carousel (native overflow
  // scrolling has no mouse-drag affordance). The preview charts never consume mouse
  // events, so a mousedown over a chart reaches the track and starts the drag.
  const dragRef = useRef({ down: false, startX: 0, startLeft: 0, moved: false });
  // A release that ends a real drag must not fire the click underneath it (e.g. the
  // expand button); one-shot, consumed by the track's click capture handler.
  const suppressClickRef = useRef(false);

  const handleDragMouseDown = (event: React.MouseEvent) => {
    if (!hasHover || event.button !== 0) return;
    const track = trackRef.current;

    if (!track) return;
    markUserInteraction();
    dragRef.current = { down: true, startX: event.clientX, startLeft: track.scrollLeft, moved: false };
  };

  const handleDragClickCapture = (event: React.MouseEvent) => {
    if (!suppressClickRef.current) return;
    suppressClickRef.current = false;
    event.preventDefault();
    event.stopPropagation();
  };

  useEffect(() => {
    if (!hasHover) return;

    const onMouseMove = (event: MouseEvent) => {
      const drag = dragRef.current;
      const track = trackRef.current;

      if (!drag.down || !track) return;
      const dx = event.clientX - drag.startX;

      if (!drag.moved && Math.abs(dx) < DRAG_THRESHOLD_PX) return;

      if (!drag.moved) {
        drag.moved = true;
        setDragging(true);
        // mandatory snap fights a free-form drag; lift it until the release
        track.style.scrollSnapType = 'none';
      }
      track.scrollLeft = drag.startLeft - dx;
    };

    const onMouseUp = () => {
      const drag = dragRef.current;
      const track = trackRef.current;

      if (!drag.down) return;
      const wasDrag = drag.moved;
      drag.down = false;

      if (!wasDrag || !track) return;
      markUserInteraction();
      setDragging(false);
      // swallow the click that trails a real drag; self-clears because a release outside
      // the track fires no click at all
      suppressClickRef.current = true;
      window.setTimeout(() => { suppressClickRef.current = false; }, 200);
      // ease onto the nearest slide while snap is still off, then re-enable the CSS snap
      scrollToIndex(nearestIndex());
      window.setTimeout(() => {
        if (trackRef.current) trackRef.current.style.scrollSnapType = '';
      }, 400);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);

    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, [hasHover, markUserInteraction, nearestIndex, scrollToIndex]);

  return (
    <div>
      <div
        ref={trackRef}
        onScroll={handleScroll}
        onPointerDown={markUserInteraction}
        onWheel={markUserInteraction}
        onMouseDown={handleDragMouseDown}
        onClickCapture={handleDragClickCapture}
        onMouseEnter={() => { hoveredRef.current = true; }}
        onMouseLeave={() => { hoveredRef.current = false; }}
        className={cn(
          'relative flex snap-x snap-mandatory gap-4 overflow-x-auto pb-1 [-ms-overflow-style:none] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden',
          // mouse-drag panning affordance (hover devices); the cursor is pushed onto the
          // chart canvases too since they carry their own default cursor
          hasHover && 'cursor-grab select-none [&_canvas]:cursor-grab',
          dragging && 'cursor-grabbing [&_canvas]:cursor-grabbing',
        )}
      >
        {examples.map((example) => (
          <div key={example.indicator} className="w-[92%] flex-shrink-0 snap-start md:w-[70%]">
            <Card>
              <CardContent className="flex flex-col gap-3 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-mono text-base font-semibold">{example.ticker}</span>
                  {example.exchangeName && <ExchangeLogo name={example.exchangeName} />}
                  <Badge variant="secondary" className="ml-auto">{getIndicatorShortName(example.indicator)}</Badge>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="size-11"
                    aria-label={t.landing.expand}
                    onClick={() => { markUserInteraction(); setExpanded(example); }}
                  >
                    <Maximize2 className="h-5 w-5" />
                  </Button>
                </div>
                <InteractiveChart data={example.chart} hint={false} interactive={false} />
              </CardContent>
            </Card>
          </div>
        ))}
      </div>

      {/* arrows show on mobile too (explicit slide control next to swipe); the desktop
          dots are 44px click targets and would not fit a phone width, so mobile gets the
          same dots as a static position indicator (the active one stretches into a pill) */}
      <div className="mt-4 flex items-center justify-center gap-1">
        <Button variant="ghost" size="icon" className="size-11" aria-label={g.prev} onClick={() => { markUserInteraction(); scrollToIndex((active + count - 1) % count); }}>
          <ChevronLeft className="h-5 w-5" />
        </Button>
        <span aria-hidden="true" className="flex items-center gap-1.5 md:hidden">
          {examples.map((example, i) => (
            <span
              key={example.indicator}
              className={cn(
                'h-1.5 rounded-full transition-all duration-200',
                i === active ? 'w-4 bg-primary' : 'w-1.5 bg-muted-foreground/30',
              )}
            />
          ))}
        </span>
        <span className="hidden items-center md:flex">
          {examples.map((example, i) => (
            <button
              key={example.indicator}
              type="button"
              aria-label={g.goTo.replace('{{number}}', String(i + 1))}
              onClick={() => { markUserInteraction(); scrollToIndex(i); }}
              className="flex size-11 items-center justify-center"
            >
              <span className={cn(
                'h-2.5 w-2.5 rounded-full transition-colors',
                i === active ? 'bg-primary' : 'bg-muted-foreground/30 hover:bg-muted-foreground/60',
              )} />
            </button>
          ))}
        </span>
        <Button variant="ghost" size="icon" className="size-11" aria-label={g.next} onClick={() => { markUserInteraction(); scrollToIndex((active + 1) % count); }}>
          <ChevronRight className="h-5 w-5" />
        </Button>
      </div>

      <ChartDialog
        example={expanded}
        open={expanded !== null}
        onOpenChange={(open) => { if (!open) { markUserInteraction(); setExpanded(null); } }}
      />
    </div>
  );
}
