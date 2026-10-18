import { useQuery } from '@tanstack/react-query';
import { api } from '@/services/api';
import { qk } from '@/lib/query';

/**
 * How this instance is published, as the backend says it: a public deployment (the project's own
 * landing page) ships the marketing surface only - landing, indicator catalog, legal - while a
 * self-hosted installation is the whole product. The frontend reads it once and keeps the app out
 * of the way here: no sign-in or dashboard entry points, the landing's calls to action point at
 * the installation, and the app routes lead back to the landing instead of an empty login.
 *
 * An unreadable answer means the whole product: a self-hosted instance is what this software is
 * for, and a failed probe on a public box costs one button, never a working dashboard.
 */
export function useSiteMode() {
  return useQuery({
    queryKey: qk.siteConfig,
    queryFn: () => api.getSiteConfig(),
    staleTime: Infinity,
    retry: 0,
  });
}
