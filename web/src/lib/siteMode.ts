import { useQuery } from '@tanstack/react-query';
import { api } from '@/services/api';
import { qk } from '@/lib/query';
import type { SiteConfigDto } from '@/types/contracts';

declare global {
  interface Window {
    /** Set by the API when it stamps an HTML shell (Internal/SiteModeScript); absent under vite. */
    __SI_SITE_LANDING_ONLY__?: boolean;
  }
}

/**
 * How this instance is published, as the backend says it: a public deployment (the project's own
 * landing page) ships the marketing surface only - landing, indicator catalog, legal - while a
 * self-hosted installation is the whole product. The frontend reads it once and keeps the app out
 * of the way here: no sign-in or dashboard entry points, the landing's calls to action point at
 * the installation, and the app routes lead back to the landing instead of an empty login.
 *
 * The backend stamps the answer into the HTML it serves, so a backend-served instance knows its
 * shape at first paint and never renders a sign-in affordance it would have to take back; the
 * probe is what remains for environments without the stamp (the vite dev server).
 *
 * An unreadable answer means the whole product: a self-hosted instance is what this software is
 * for, and a failed probe on a public box costs one button, never a working dashboard.
 */
export function injectedSiteConfig(): SiteConfigDto | undefined {
  const flag = typeof window === 'undefined' ? undefined : window.__SI_SITE_LANDING_ONLY__;
  return typeof flag === 'boolean' ? { landingOnly: flag } : undefined;
}

export function useSiteMode() {
  return useQuery({
    queryKey: qk.siteConfig,
    queryFn: () => api.getSiteConfig(),
    staleTime: Infinity,
    retry: 0,
    // With the stamp present the query starts resolved and the probe never runs.
    initialData: injectedSiteConfig,
  });
}
