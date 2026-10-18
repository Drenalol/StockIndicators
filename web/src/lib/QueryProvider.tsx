import { useEffect, type ReactNode } from 'react';
import { QueryClientProvider, useQueryClient } from '@tanstack/react-query';
import { queryClient } from '@/lib/query';
import { useApiHealth } from '@/hooks/ApiHealthProvider';

/**
 * Heals stale errors after an API outage: every read effect used to carry the
 * health `recovered` counter in its deps; one global invalidation on a
 * down→up transition replaces all of them.
 */
function QueryHealing() {
  const { recovered } = useApiHealth();
  const qc = useQueryClient();
  useEffect(() => {
    if (recovered > 0) void qc.invalidateQueries();
  }, [recovered, qc]);
  return null;
}

export function AppQueryProvider({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <QueryHealing />
      {children}
    </QueryClientProvider>
  );
}
