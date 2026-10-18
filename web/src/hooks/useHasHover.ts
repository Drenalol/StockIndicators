import { useEffect, useState } from 'react';

const HOVER_QUERY = '(hover: hover) and (pointer: fine)';

/**
 * True when the primary input supports fine-pointer hover (mouse/trackpad).
 * Radix Tooltip never opens on touch by design, so hover-only UI must be
 * feature-detected (media query), NOT inferred from viewport width.
 */
export function useHasHover(): boolean {
  const [hasHover, setHasHover] = useState(() => {
    try {
      return window.matchMedia(HOVER_QUERY).matches;
    } catch {
      return true;
    }
  });

  useEffect(() => {
    let mql: MediaQueryList;
    try {
      mql = window.matchMedia(HOVER_QUERY);
    } catch {
      return;
    }
    setHasHover(mql.matches);
    const onChange = (event: MediaQueryListEvent) => setHasHover(event.matches);
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, []);

  return hasHover;
}
