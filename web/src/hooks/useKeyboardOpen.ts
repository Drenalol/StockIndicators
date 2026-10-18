import { useEffect, useState } from 'react';

// Soft-keyboard detection for mobile WebViews (iOS/Telegram): the keyboard shrinks the
// visual viewport while the layout viewport (and 100dvh) stays full-height, so the
// sticky bottom chrome ends up behind the keys and the form loses almost all of its
// space. On Android WebView (native keyboard resize) the window itself shrinks and the
// two viewports stay equal - the hook stays inactive there and the layout adapts by
// itself.
//
// While open, the hook writes the inset into --kb-inset and flags <html data-keyboard>
// (see index.css): the page-root height tokens subtract the inset and the tab bar /
// fixed action bars collapse (--nav-h/--bar-h to 0, components hide them).
export function useKeyboardOpen(): boolean {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const vv = window.visualViewport;
    if (!vv) return;
    let wasOpen = false;
    const update = () => {
      const inset = Math.max(0, window.innerHeight - vv.height);
      const next = inset >= 100;
      const root = document.documentElement;
      root.style.setProperty('--kb-inset', next ? `${Math.round(inset)}px` : '0px');
      root.dataset.keyboard = next ? 'true' : 'false';
      // iOS auto-scrolls the layout viewport to reveal the focused field; once the
      // keyboard inset is folded into the layout (and the bottom chrome collapses),
      // that stale window scroll leaves the whole page scrolled past its own
      // content - a blank screen. Snap the window back to the top on open.
      if (next && !wasOpen) window.scrollTo(0, 0);
      wasOpen = next;
      setOpen(was => (was === next ? was : next));
    };
    update();
    vv.addEventListener('resize', update);
    vv.addEventListener('scroll', update);
    return () => {
      vv.removeEventListener('resize', update);
      vv.removeEventListener('scroll', update);
      document.documentElement.style.removeProperty('--kb-inset');
      delete document.documentElement.dataset.keyboard;
    };
  }, []);

  return open;
}
