import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { checkApiHealth } from '@/services/api';

export type ApiHealthStatus = 'checking' | 'up' | 'down';

interface ApiHealthValue {
  status: ApiHealthStatus;
  offline: boolean;
  /** Increments on every down→up transition so pages can refetch and heal stale errors. */
  recovered: number;
}

const RETRY_INTERVAL_MS = 5000;
const WATCH_INTERVAL_MS = 10000;

const ApiHealthContext = createContext<ApiHealthValue>({ status: 'checking', offline: false, recovered: 0 });

/**
 * Watches the API health endpoint: periodic checks while the service is up (so
 * a mid-session outage raises the overlay too) and fast retries while it is
 * down. Reacts to online/offline events and to becoming visible again with an
 * immediate re-check (VPN toggles keep navigator.onLine true, so the visible +
 * watch loop is what catches those).
 */
export function ApiHealthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<ApiHealthStatus>('checking');
  const [offline, setOffline] = useState(() => !navigator.onLine);
  const [recovered, setRecovered] = useState(0);
  const statusRef = useRef(status);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    statusRef.current = status;
  }, [status]);

  useEffect(() => {
    let active = true;

    const stopTimer = () => {
      if (timer.current) {
        clearTimeout(timer.current);
        timer.current = null;
      }
    };

    const run = async () => {
      stopTimer();
      const wasDown = statusRef.current === 'down';
      const up = await checkApiHealth();
      if (!active) return;
      setStatus(up ? 'up' : 'down');
      if (up && wasDown) setRecovered((c) => c + 1);
      timer.current = setTimeout(() => void run(), up ? WATCH_INTERVAL_MS : RETRY_INTERVAL_MS);
    };

    const onOnline = () => {
      setOffline(false);
      void run();
    };

    const onOffline = () => {
      setOffline(true);
      setStatus('down');
      void run();
    };

    const onVisible = () => {
      if (document.visibilityState === 'visible') void run();
    };

    window.addEventListener('online', onOnline);
    window.addEventListener('offline', onOffline);
    document.addEventListener('visibilitychange', onVisible);
    void run();

    return () => {
      active = false;
      stopTimer();
      window.removeEventListener('online', onOnline);
      window.removeEventListener('offline', onOffline);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);

  const value = useMemo(() => ({ status, offline, recovered }), [status, offline, recovered]);

  return <ApiHealthContext.Provider value={value}>{children}</ApiHealthContext.Provider>;
}

export function useApiHealth(): ApiHealthValue {
  return useContext(ApiHealthContext);
}
