import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useReducedMotion } from 'motion/react';
import { useTranslation } from '@/i18n';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

// Generic spotlight walkthrough, shown once per account (flags live on the server):
// the nav tour over the menu items (MainLayout) and the create-subscription tour
// over the form sections (SubscriptionFormPage). Anchors are marked with data-tour.
export interface TourStep {
  anchor: string;
  title: string;
  text: string;
}

const PADDING = 6;
const GAP = 12;
const EDGE = 12;
const CARD_WIDTH = 320;
const CARD_ESTIMATED_HEIGHT = 240;
const ANCHOR_RETRY_MS = 5000;
const MIN_RING_HEIGHT = 24;
// the strip must show the anchor's title row, not a slice of its body
const MIN_HEAD_VISIBLE = 48;
// content (presets catalog, condition rows, notifications) keeps loading while a
// step is open and moves anchors under the card; remeasure and re-room periodically
const RESTYLE_MS = 600;

interface Band {
  top: number;
  bottom: number;
}

const findScroller = (el: HTMLElement): HTMLElement | null => {
  let node = el.parentElement;
  while (node && node !== document.body) {
    const overflowY = getComputedStyle(node).overflowY;
    if ((overflowY === 'auto' || overflowY === 'scroll') && node.scrollHeight > node.clientHeight) return node;
    node = node.parentElement;
  }
  return null;
};

// The visible band of the page's own scroll container: the sticky header and the
// fixed bottom nav/action bar overlay the raw viewport, so the room the anchor and
// the card can actually share is the container's client box, not window.innerHeight
const bandOf = (scroller: HTMLElement | null): Band => {
  if (!scroller) return { top: 0, bottom: window.innerHeight };
  const box = scroller.getBoundingClientRect();
  return { top: Math.max(0, box.top), bottom: Math.min(window.innerHeight, box.bottom) };
};

// The step is settled only when the whole anchor is inside the page's visible band
// AND a card-sized free strip already exists on one of its sides. "A side has room"
// alone is not a reason to leave the page as is: that room is filled with other
// content, and an anchor whose body dangles under the fixed action bar is not
// placed just because its head peeks into the band. An unsettled step aligns the
// anchor's head with the band top (an absolute, idempotent target, so re-runs on
// card-height changes do not compound the scroll); the card then covers whatever
// body does not fit, and the render-side scoring lights the head on the free side.
const makeRoom = (anchor: HTMLElement, cardHeight: number) => {
  const scroller = findScroller(anchor);
  if (!scroller) return;
  const band = bandOf(scroller);
  const rect = anchor.getBoundingClientRect();
  const bodyInside = rect.bottom <= band.bottom + 1;
  const roomBelow = band.bottom - rect.bottom - PADDING - GAP >= cardHeight + EDGE;
  const roomAbove = rect.top - band.top - PADDING - GAP >= cardHeight + EDGE;
  if (rect.top >= band.top - 1 && bodyInside && (roomBelow || roomAbove)) return;
  const maxScroll = scroller.scrollHeight - scroller.clientHeight;
  scroller.scrollTop = Math.min(Math.max(scroller.scrollTop + rect.top - band.top, 0), maxScroll);
};

interface OnboardingTourProps {
  open: boolean;
  steps: readonly TourStep[];
  onDone: () => void;
}

export default function OnboardingTour({ open, steps, onDone }: OnboardingTourProps) {
  const { t } = useTranslation();
  const reducedMotion = useReducedMotion();
  const [step, setStep] = useState(0);
  const [rect, setRect] = useState<DOMRect | null>(null);
  const cardRef = useRef<HTMLDivElement>(null);
  const [cardHeight, setCardHeight] = useState(CARD_ESTIMATED_HEIGHT);

  // the clamping below must use the real card height: a too-small estimate would let
  // the card overlap the spotlight on short viewports (mobile)
  useLayoutEffect(() => {
    if (open && cardRef.current?.offsetHeight) setCardHeight(cardRef.current.offsetHeight);
  }, [open, step]);

  useEffect(() => {
    if (!open) {
      setStep(0);
      return;
    }
    let raf = 0;
    let deadline = 0;
    const update = (scroll: boolean) => {
      const current = steps[step];
      if (!current) {
        setRect(null);
        return false;
      }
      // Anchors exist in both nav bars / action bars (header on desktop, bottom bar on
      // mobile); the hidden one has a zero-size rect, so pick the visible anchor.
      // A zero *height* means the anchored block has no content yet (e.g. the preset
      // wrapper while its catalog is still loading) - that is "not found", not a
      // degenerate spotlight, so the retry loop keeps waiting for it.
      const target = Array
        .from(document.querySelectorAll<HTMLElement>(`[data-tour="${current.anchor}"]`))
        .find(el => {
          const r = el.getBoundingClientRect();
          return r.width > 0 && r.height > 0;
        });
      if (target && scroll) {
        // form sections may sit below the fold of the page's inner scroll container or
        // be too tall for the visible band; open a card-sized strip before measuring
        // (no global scroll-behavior is set, so the scroll applies instantly)
        makeRoom(target, cardHeight);
      }
      // the card/spotlight geometry is derived from this rect in render, so an equal
      // rect keeps the placement as is (the remeasure interval would otherwise
      // re-render the tour several times a second forever)
      setRect(prev => {
        const next = target ? target.getBoundingClientRect() : null;
        if (!next) return prev === null ? prev : null;
        if (prev && prev.top === next.top && prev.left === next.left && prev.width === next.width && prev.height === next.height) return prev;
        return next;
      });
      return !!target;
    };
    // the tour can open while the page still renders its data skeleton (no anchors
    // in the DOM yet); keep looking for a while so the spotlight lands once content mounts
    const retry = () => {
      if (!update(true) && performance.now() < deadline) raf = requestAnimationFrame(retry);
    };
    deadline = performance.now() + ANCHOR_RETRY_MS;
    retry();
    // lazily loaded content shifts anchors under the open card (the page cannot be
    // scrolled by the user: the blocker overlay swallows touches) - remeasure and
    // re-room so the card never ends up sitting on top of a moved anchor
    const restyle = window.setInterval(() => update(true), RESTYLE_MS);
    const onResize = () => update(false);
    window.addEventListener('resize', onResize);
    return () => {
      cancelAnimationFrame(raf);
      window.clearInterval(restyle);
      window.removeEventListener('resize', onResize);
    };
  }, [open, step, steps, cardHeight]);

  if (!open || steps.length === 0) return null;

  const content = steps[step];
  const first = step === 0;
  const last = step === steps.length - 1;

  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const cardWidth = Math.min(CARD_WIDTH, viewportWidth - EDGE * 2);
  const cardLeft = rect
    ? Math.min(Math.max(rect.left + rect.width / 2 - cardWidth / 2, EDGE), viewportWidth - cardWidth - EDGE)
    : EDGE;
  // a tall anchor (a whole form section) must not push the card off-screen:
  // clamp the offset from the anchor edge by the real card height. Both sides are
  // then placed and scored: the winner is the side whose lit strip keeps the anchor's
  // title row visible (the head is what tells the user which block is highlighted),
  // and between two head-visible sides the taller strip wins. Clamping the chosen
  // side only after picking it is what stops the card from being pinned into the
  // dead viewport space above an anchor the page can no longer lift (maxScroll of
  // the scroll container is reached below a below-the-fold anchor's own head).
  const headTop = rect ? Math.max(rect.top - PADDING, 0) : 0;
  const maxCardOffset = viewportHeight - cardHeight - EDGE;
  const belowOffset = rect ? Math.max(EDGE, Math.min((rect.bottom + GAP + PADDING), maxCardOffset)) : EDGE;
  const aboveOffset = rect ? Math.max(EDGE, Math.min(viewportHeight - rect.top + GAP + PADDING, maxCardOffset)) : EDGE;
  const belowCardTop = belowOffset;
  const aboveCardTop = viewportHeight - aboveOffset - cardHeight;
  const belowHole = rect ? belowCardTop - GAP - headTop : 0;
  const aboveHole = rect ? Math.min(rect.bottom + PADDING, viewportHeight) - Math.max(headTop, aboveCardTop + cardHeight + GAP) : 0;
  // the below-card strip starts at the anchor head, so its height is the visible head
  const headSeenBelow = belowHole >= MIN_HEAD_VISIBLE;
  const headSeenAbove = aboveCardTop + cardHeight + GAP <= headTop + MIN_HEAD_VISIBLE;
  // when both placements keep the head lit the taller strip wins; when only one does
  // that side wins; when neither can, the side with more free viewport around the
  // anchor (a bottom-nav anchor of the nav tour sits at the very screen bottom, so
  // its card must go above it even though the head is technically "near" both sides)
  const below = !rect
    || (headSeenBelow && headSeenAbove ? belowHole >= aboveHole
      : headSeenBelow ? true
        : headSeenAbove ? false
          : viewportHeight - rect.bottom >= rect.top);
  const cardOffset = below ? belowOffset : aboveOffset;
  const cardTop = below ? belowCardTop : aboveCardTop;
  const cardBottom = cardTop + cardHeight;
  // the dimming is the spotlight ring's own 9999px box-shadow, so the ring must keep
  // rendering even when the card steals part of the anchor: the hole starts where the
  // card ends (below the card when the card sits above, above it when below)
  const holeTop = rect ? Math.max(rect.top - PADDING, below ? 0 : cardBottom + GAP) : 0;
  const holeBottom = rect ? Math.min(rect.bottom + PADDING, below ? cardTop - GAP : viewportHeight) : 0;

  return (
    <>
      {/* swallows every click while the tour is open */}
      <div className="fixed inset-0 z-[65]" aria-hidden />

      {rect && holeBottom - holeTop >= MIN_RING_HEIGHT && (
        <div
          className={cn(
            'fixed z-[70] rounded-lg ring-2 ring-primary/60',
            !reducedMotion && 'transition-all duration-200 ease-out'
          )}
          style={{
            top: holeTop,
            left: rect.left - PADDING,
            width: rect.width + PADDING * 2,
            // a section taller than the free band strip is spotlighted only as far
            // as the card allows, so the ring never hides under the card
            height: holeBottom - holeTop,
            boxShadow: '0 0 0 9999px rgb(0 0 0 / 0.55)',
          }}
          aria-hidden
        />
      )}

      <div
        ref={cardRef}
        role="dialog"
        aria-label={content.title}
        className={cn(
          'fixed z-[75] rounded-xl border bg-card p-4 text-card-foreground shadow-xl',
          !reducedMotion && 'animate-in fade-in-0 zoom-in-95 duration-200'
        )}
        style={{
          width: cardWidth,
          left: cardLeft,
          ...(below ? { top: cardOffset } : { bottom: cardOffset }),
        }}
      >
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-sm font-semibold">{content.title}</h2>
          <span className="text-xs text-muted-foreground tabular-nums">{step + 1} / {steps.length}</span>
        </div>
        <p className="mt-1 text-sm text-muted-foreground">{content.text}</p>
        <div className="mt-3 flex items-center justify-between gap-2">
          <Button variant="ghost" size="sm" onClick={onDone}>{t.onboarding.skip}</Button>
          <div className="flex items-center gap-2">
            {!first && (
              <Button variant="outline" size="sm" onClick={() => setStep(step - 1)}>
                {t.onboarding.back}
              </Button>
            )}
            <Button
              size="sm"
              onClick={() => (last ? onDone() : setStep(step + 1))}
            >
              {last ? t.onboarding.done : t.onboarding.next}
            </Button>
          </div>
        </div>
      </div>
    </>
  );
}
