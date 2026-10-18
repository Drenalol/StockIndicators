import { useEffect, useRef, useState } from 'react';
import { useReducedMotion } from 'motion/react';
import { cn } from '@/lib/utils';

// A section is active once its top edge has scrolled up to this distance from the
// viewport top (the 81px sticky header plus a small margin). Tick clicks align the
// section top at 96px (scroll-mt), so a click always lands inside the clicked
// section's range - unlike a viewport-middle band, which a short section ends above
const SECTION_LINE_PX = 130;

// Distance from the document bottom still counted as "at the bottom"
// (fractional scroll positions on zoomed displays)
const BOTTOM_EPSILON_PX = 2;

// Keys that scroll the page: one of them down means the user took over the scrolling
const SCROLL_KEYS = [' ', 'ArrowDown', 'ArrowUp', 'End', 'Home', 'PageDown', 'PageUp'];

// The programmatic click-scroll counts as landed after this quiet time, and any further
// movement from the landing point (a scrollbar drag: wheel/touch/keys have their own
// listeners) hands the active state back to the spy
const REST_SETTLE_MS = 150;
const REST_MOVE_PX = 6;

export interface SectionTocItem {
  id: string;
  label: string;
}

/**
 * Landing section navigation: fixed ticks on the right edge that show the section
 * title on hover and light the section whose top is currently at the viewport top;
 * a click scrolls to the section. Two cases a "middle of the viewport band" spy gets
 * wrong: a section can be shorter than the landing offset of a click (aligning its
 * top at the top leaves the band over the NEXT section), and over a short tail the
 * last section's top can never reach any fixed line at all. So the spy is top-line
 * based, the document bottom always lights the last tick, and a clicked tick wins
 * over the spy until the user takes over the scrolling (wheel, touch, scroll keys or
 * a further move past the landing point) - otherwise a click near the bottom (whose
 * scroll clamps at the end) would relight the last section instead of the one clicked.
 * Desktop-only: on a phone the labels are hover-only and the 44px touch targets
 * would eat the screen edge and fight the swipe gestures.
 */
export function SectionToc({ items, label }: { items: SectionTocItem[]; label: string }) {
  const reducedMotion = useReducedMotion();
  const [active, setActive] = useState(items[0]?.id ?? '');
  // the landing sections are conditional (the gallery) - rebuild the spy only when the
  // set of ids actually changed, not on every landing re-render
  const idsKey = items.map((item) => item.id).join('|');
  // the clicked tick: keeps its light over the spy until the user scrolls manually
  const forcedRef = useRef('');
  // the position the click's programmatic scroll settled at, and whether it settled yet
  const restedRef = useRef(false);
  const restPosRef = useRef(0);
  const restTimerRef = useRef(0);

  useEffect(() => {
    const ids = idsKey.split('|');

    if (ids.length === 0) return;

    const compute = () => {
      const line = window.scrollY + SECTION_LINE_PX;
      let current = ids[0];

      for (const id of ids) {
        const el = document.getElementById(id);

        if (el && el.getBoundingClientRect().top + window.scrollY <= line) current = id;
      }

      // there is nothing to scroll past the end: the document bottom is the last
      // section's moment, whatever the line says
      if (
        window.innerHeight + window.scrollY >=
        document.documentElement.scrollHeight - BOTTOM_EPSILON_PX
      ) {
        current = ids[ids.length - 1];
      }

      return current;
    };

    const onScroll = () => {
      if (!forcedRef.current) {
        setActive(compute());
        return;
      }

      if (restedRef.current) {
        // scrolling again after the click's landing point is user-driven (a scrollbar
        // drag: wheel/touch/keys clear the override in their own listeners), so the
        // spy takes the active state back
        if (Math.abs(window.scrollY - restPosRef.current) > REST_MOVE_PX) {
          forcedRef.current = '';
          setActive(compute());
        }
        return;
      }

      // the programmatic animation keeps firing scroll events until it lands; the
      // clicked tick holds the light across all of them
      window.clearTimeout(restTimerRef.current);
      restTimerRef.current = window.setTimeout(() => {
        restPosRef.current = window.scrollY;
        restedRef.current = true;
      }, REST_SETTLE_MS);
    };

    const release = () => {
      forcedRef.current = '';
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (SCROLL_KEYS.includes(event.key)) release();
    };

    // an initial render already at some scroll position (restored scroll, anchor
    // jump) fires no scroll event, so compute once
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll);
    // wheel/touch/scroll-keys are always user-originated: from the first one of them
    // the page is the user's again and the spy resumes
    window.addEventListener('wheel', release, { passive: true });
    window.addEventListener('touchstart', release, { passive: true });
    window.addEventListener('keydown', onKeyDown);

    return () => {
      window.removeEventListener('scroll', onScroll);
      window.removeEventListener('resize', onScroll);
      window.removeEventListener('wheel', release);
      window.removeEventListener('touchstart', release);
      window.removeEventListener('keydown', onKeyDown);
      window.clearTimeout(restTimerRef.current);
    };
  }, [idsKey]);

  const scrollTo = (id: string) => {
    forcedRef.current = id;
    restedRef.current = false;
    window.clearTimeout(restTimerRef.current);
    setActive(id);
    document.getElementById(id)?.scrollIntoView({ behavior: reducedMotion ? 'auto' : 'smooth' });
  };

  return (
    // shown only when the side margin can host it: the page content is max-w-5xl (1024px)
    // centered, so below ~1440px viewport the active/hover title would sit on top of the
    // content instead of the free margin (208px side margin - 16px offset vs ~175px
    // longest label + ticks); the ticks strip itself is ~150px tall, no height gate needed
    <nav aria-label={label} className="fixed right-4 top-1/2 z-30 hidden -translate-y-1/2 flex-col items-end gap-1 min-[1440px]:flex">
      {items.map((item) => {
        const isActive = item.id === active;

        return (
          <button
            key={item.id}
            type="button"
            aria-label={item.label}
            aria-current={isActive ? 'true' : undefined}
            onClick={() => scrollTo(item.id)}
            className="group flex items-center justify-end gap-2 py-1.5"
          >
            <span
              className={cn(
                'text-[11px] font-medium uppercase tracking-[0.18em] transition-opacity',
                isActive ? 'text-foreground opacity-100' : 'text-muted-foreground opacity-0 group-hover:opacity-100',
              )}
            >
              {item.label}
            </span>
            <span
              className={cn(
                'h-px transition-all duration-200',
                isActive ? 'w-7 bg-primary' : 'w-4 bg-muted-foreground/40 group-hover:w-6 group-hover:bg-muted-foreground',
              )}
            />
          </button>
        );
      })}
    </nav>
  );
}
