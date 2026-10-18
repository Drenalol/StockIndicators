import { useEffect, useRef, useState } from 'react';

/**
 * Paint gate for freshly arrived list data. React commits a long list in one render,
 * but a big DOM paints progressively inside the scroller (the browser fills the layer
 * top to bottom), which reads as the table "populating" right after the skeleton is
 * removed. While `busy` is true the caller keeps the skeleton on screen and renders
 * the fresh rows invisible; after two animation frames (the first paint guaranteed)
 * the gate opens and the caller fades the drawn list in.
 *
 * The gate keys on the identity of the react-query cache entry, not on derived arrays:
 * client-side filtering and sorting must stay instant, and the default structural
 * sharing keeps background refetches of unchanged data from re-gating. A mount that
 * finds warm cached data starts painted, so returning to a cached page never flashes
 * a skeleton (the pattern the pages already promise for revisits).
 */
export function useListPainted(data: unknown, count: number): { painted: boolean; busy: boolean } {
  const [painted, setPainted] = useState(count > 0);
  const committed = useRef(data);

  useEffect(() => {
    if (committed.current === data) return;

    committed.current = data;
    setPainted(false);

    let second = 0;

    const first = requestAnimationFrame(() => {
      second = requestAnimationFrame(() => setPainted(true));
    });

    return () => {
      cancelAnimationFrame(first);
      cancelAnimationFrame(second);
    };
  }, [data]);

  return { painted, busy: !painted && count > 0 };
}
