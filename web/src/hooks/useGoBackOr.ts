import { useCallback } from 'react';
import { useNavigate, type To } from 'react-router-dom';
import { canGoBackInApp } from '@/lib/history';

// The in-app back arrow must behave like the browser's back: pop the previous
// entry when the app itself created it. A fresh load/deep link has nothing to
// pop, so go to the logical parent instead; replace:true keeps the arrow from
// cycling back into the page the user just left.
export function useGoBackOr() {
  const navigate = useNavigate();
  return useCallback(
    (fallback: To) => {
      if (canGoBackInApp()) {
        navigate(-1);
      } else {
        navigate(fallback, { replace: true });
      }
    },
    [navigate],
  );
}
