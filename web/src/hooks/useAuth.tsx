import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { account, auth, isTransientAuthError, setOnUnauthorized } from '@/services/api';
import { useApiHealth } from '@/hooks/ApiHealthProvider';
import { toast } from '@/hooks/use-toast';
import { getBrowserTimezone } from '@/lib/timezones';
import { queryClient } from '@/lib/query';
import { useTranslation, SUPPORTED_LOCALES, type Locale } from '@/i18n';
import type { AuthProfileDto } from '@/types/contracts';

/**
 * Non-credential hint that the last resolved session was authenticated. The
 * session itself is an httpOnly cookie, so nothing on the page can know it
 * before GET /auth/me returns, and the landing header would otherwise flash
 * "Sign in" for every signed-in visitor and only then flip to "Open the app".
 * The hint carries no token and is never trusted for authorization: it only
 * picks the landing button label while the probe is still in flight, and every
 * real guard keeps using isAuthenticated/loaded.
 */
const SESSION_HINT_KEY = 'si_session_hint';

function readSessionHint(): boolean {
  if (typeof window === 'undefined') return false;
  try {
    return localStorage.getItem(SESSION_HINT_KEY) === '1';
  } catch {
    // Private mode / storage disabled - the landing just falls back to "Sign in"
    return false;
  }
}

function writeSessionHint(authenticated: boolean): void {
  if (typeof window === 'undefined') return;
  try {
    if (authenticated) localStorage.setItem(SESSION_HINT_KEY, '1');
    else localStorage.removeItem(SESSION_HINT_KEY);
  } catch {
    // Storage disabled - nothing to remember
  }
}

interface AuthContextValue {
  profile: AuthProfileDto | null;
  loaded: boolean;
  loading: boolean;
  isAuthenticated: boolean;
  /** Same as isAuthenticated, plus a persisted hint trusted until the first /auth/me of this load resolves */
  looksAuthenticated: boolean;
  /** The instance password; a wrong one throws (401), the caller renders the error */
  loginWithPassword: (password: string) => Promise<void>;
  clear: () => Promise<void>;
  refresh: () => Promise<void>;
  completeOnboarding: () => Promise<void>;
  completeSubscriptionTour: () => Promise<void>;
  setChartPreferences: (patch: { ohlcTooltip?: boolean; magnet?: boolean }) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Single auth state owner for the whole app. The session is one httpOnly cookie
 * issued against the password in the instance configuration; there is nothing to
 * refresh and nobody else to be. The provider refreshes the profile once per load
 * and in-flight calls are deduplicated.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<AuthProfileDto | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [loading, setLoading] = useState(false);
  // Snapshot of the persisted hint, taken once before any probe resolves
  const [hadSessionHint] = useState(readSessionHint);
  const { locale, applyServerLanguage, t } = useTranslation();
  // Read through a ref so `refresh` keeps a stable identity and the mount
  // effect does not re-fire on locale changes.
  const localeRef = useRef(locale);
  localeRef.current = locale;
  const applyServerLanguageRef = useRef(applyServerLanguage);
  applyServerLanguageRef.current = applyServerLanguage;
  const tRef = useRef(t);
  tRef.current = t;
  // Current profile for the async 401 handler (see handleUnauthorized below)
  const profileRef = useRef(profile);
  profileRef.current = profile;
  const refreshPromiseRef = useRef<Promise<void> | null>(null);
  // One-shot guard for a sign-out episode. The profile ref above is only
  // refreshed on re-render, so when several requests 401 in the same burst (e.g.
  // everything a page has in flight at once), each one would pass their check and
  // fire its own toast. This ref makes the sign-out idempotent until the user
  // signs back in (reset in refresh/loginWithPassword).
  const unauthorizedHandledRef = useRef(false);

  /**
   * Applies the server profile to the UI prefs: a language the server has but
   * the client has not synchronized yet (a change made in the settings page or
   * in another browser) overrides the locally detected locale. A language the
   * marker already knows is a profile that did not change, so the local choice
   * stays: a landing/login language switch must survive a reload without ever
   * being written back to the profile. Fills language/timezone from the browser
   * when the account never had them set (one-shot: the fields are non-null
   * afterwards). Best-effort - failures are ignored, the user can always set
   * them manually in the profile.
   */
  const fillMissingPrefs = useCallback(async (me: AuthProfileDto): Promise<AuthProfileDto> => {
    if (me.language !== null) {
      const lang = me.language as Locale;
      if (SUPPORTED_LOCALES.includes(lang)) {
        applyServerLanguageRef.current(lang);
      }
    }
    const body: { language?: string; timezone?: string } = {};
    if (me.language === null) body.language = localeRef.current;
    if (me.timezone === null) {
      const tz = getBrowserTimezone();
      if (tz) body.timezone = tz;
    }
    if (Object.keys(body).length === 0) return me;
    try {
      return await account.updatePreferences(body);
    } catch {
      return me;
    }
  }, []);

  /**
   * Resolves the current session with a single GET /auth/me. A missing or expired
   * cookie answers 401 → null. A transient probe failure (API outage) leaves the
   * state unresolved (loaded stays false) instead of signing the user out.
   */
  const refresh = useCallback(async () => {
    if (refreshPromiseRef.current) return refreshPromiseRef.current;
    const run = (async () => {
      setLoading(true);
      let resolved = true;
      try {
        const me = await auth.me();
        if (me) {
          unauthorizedHandledRef.current = false;
          setProfile(await fillMissingPrefs(me));
          return;
        }
        setProfile(null);
      } catch (e) {
        // An API outage (no connection, proxy 502, timeout) proves nothing about
        // the session: stay "not loaded" so the layout guard does not throw the
        // user to the login page. The health-recovery retry below resolves the
        // state without a manual reload.
        if (!isTransientAuthError(e)) throw e;
        resolved = false;
        throw e;
      } finally {
        setLoading(false);
        if (resolved) setLoaded(true);
      }
    })();
    refreshPromiseRef.current = run;
    void run.catch(() => undefined).finally(() => {
      refreshPromiseRef.current = null;
    });
    return run;
  }, [fillMissingPrefs]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  // Persist the hint only once something was actually confirmed: while the very
  // first probe is still in flight the profile is legitimately null, and writing
  // then would erase the hint the landing header needs. A sign-in resolves the
  // hint immediately (login does not flip `loaded`), a resolved sign-out - whether
  // from the probe or the async 401 handler - clears it, so the next visit shows
  // "Sign in" again.
  useEffect(() => {
    if (!loaded && profile === null) return;
    writeSessionHint(profile !== null);
  }, [loaded, profile]);

  // Heal a session left unresolved by an API outage: re-probe /auth/me as soon as
  // the health monitor says "up" and keep re-probing on the monitor's retry cadence
  // while the probe still fails (refresh() deduplicates in-flight calls; a genuine
  // 401 answers null, so the loop cannot spin for an actually signed-out user).
  const { status } = useApiHealth();
  useEffect(() => {
    if (loaded || status !== 'up') return;
    void refresh();
    const timer = setInterval(() => void refresh(), 5000);
    return () => clearInterval(timer);
  }, [loaded, status, refresh]);

  /**
   * Called by api.ts when any authenticated request gets a 401: the cookie is gone
   * or expired. Sign out local state immediately — MainLayout's guard then
   * redirects to the login page, no F5 needed.
   */
  const handleUnauthorized = useCallback(() => {
    // Not signed in (initial probe, stray request after sign-out) — nothing to do
    if (profileRef.current === null) return;
    // Already signed out by an earlier 401 in the same burst — one toast per episode
    if (unauthorizedHandledRef.current) return;
    unauthorizedHandledRef.current = true;
    setProfile(null);
    // Same reason as in clear(): the expired session's cached rows must not be
    // handed to the next person that signs in through this SPA
    queryClient.clear();
    toast({ title: tRef.current.auth.sessionExpired, duration: 5000 });
  }, []);

  useEffect(() => {
    setOnUnauthorized(handleUnauthorized);
    return () => setOnUnauthorized(null);
  }, [handleUnauthorized]);

  /** Verifies the instance password and adopts the session cookie the response sets. */
  const loginWithPassword = useCallback(async (password: string): Promise<void> => {
    setLoading(true);
    try {
      const me = await auth.login(password);
      unauthorizedHandledRef.current = false;
      setProfile(await fillMissingPrefs(me));
    } finally {
      setLoading(false);
    }
  }, [fillMissingPrefs]);

  const clear = useCallback(async () => {
    // The logout probe itself 401s when there is no session to clear —
    // pause the global 401 handler so a voluntary sign-out is not reported as
    // "session ended" by the handler's toast.
    setOnUnauthorized(null);
    try {
      await auth.logout();
    } catch {
      // No session to clear — local state is cleared below
    } finally {
      setOnUnauthorized(handleUnauthorized);
    }
    setProfile(null);
    // Drop the whole react-query cache: it holds the previous session's rows
    // (subscriptions, provider keys…) which would otherwise be served from the
    // 30 s fresh window after a logout→login round trip
    queryClient.clear();
  }, [handleUnauthorized]);

  /**
   * Marks the first-login onboarding tour as completed/skipped on the server
   * (idempotent PATCH) and syncs the profile from the response. Best-effort:
   * on failure the tour simply shows again on the next load.
   */
  const completeOnboarding = useCallback(async () => {
    try {
      const me = await account.updatePreferences({ completeOnboarding: true });
      setProfile(me);
    } catch {
      // Ignore — a stray tour is acceptable
    }
  }, []);

  /**
   * Marks the create-subscription page tour as completed/skipped on the server
   * (idempotent PATCH) and syncs the profile from the response. Best-effort:
   * on failure the tour simply shows again on the next visit.
   */
  const completeSubscriptionTour = useCallback(async () => {
    try {
      const me = await account.updatePreferences({ completeSubscriptionTour: true });
      setProfile(me);
    } catch {
      // Ignore — a stray tour is acceptable
    }
  }, []);

  /**
   * Persists interactive-chart display preferences (partial PATCH, merged server-side
   * flag-by-flag) and syncs the profile from the response. Best-effort: on failure the
   * toggles just fall back to the stored values on the next load.
   */
  const setChartPreferences = useCallback(async (patch: { ohlcTooltip?: boolean; magnet?: boolean }): Promise<void> => {
    try {
      const me = await account.updatePreferences({ chartPreferences: patch });
      setProfile(me);
    } catch {
      // Ignore — the chart keeps working with the previously stored preferences
    }
  }, []);

  const isAuthenticated = profile !== null;
  // Landing-only label choice: before the first probe of this page load answers,
  // a persisted hint is trusted so a returning signed-in visitor never sees the
  // "Sign in" flash. Guards and data fetching keep using isAuthenticated/loaded.
  const looksAuthenticated = isAuthenticated || (!loaded && hadSessionHint);

  const value = useMemo<AuthContextValue>(() => ({
    profile, loaded, loading, isAuthenticated, looksAuthenticated,
    loginWithPassword, clear, refresh, completeOnboarding, completeSubscriptionTour, setChartPreferences,
  }), [profile, loaded, loading, isAuthenticated, looksAuthenticated, loginWithPassword, clear, refresh, completeOnboarding, completeSubscriptionTour, setChartPreferences]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
