// React Router stores its internal history index in window.history.state, so a
// raw pushState/replaceState that wipes it corrupts in-app back detection for
// the whole session. All back-capability checks go through this helper, and
// only router navigation (useNavigate/useSearchParams) may touch the history.
export function canGoBackInApp(): boolean {
  const state = window.history.state as { idx?: number } | null;
  return typeof state?.idx === 'number' && state.idx > 0;
}
