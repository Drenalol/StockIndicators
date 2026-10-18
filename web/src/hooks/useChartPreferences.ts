import { useEffect, useSyncExternalStore } from 'react';
import type { ChartPreferencesDto } from '@/types/contracts';
import { useAuth } from '@/hooks/useAuth';

// A local override keeps the toggles instant while the persisted PATCH is in flight.
// It lives at module scope (a tiny store, not component state) because the button
// and the chart are no longer the same instance: on the fullscreen dialog the gear
// sits in the dialog's title row while a nested chart component draws the canvas -
// both read the same effective document without prop drilling. The override resets
// whenever the stored profile document changes (own response, reload, another
// device). Defaults when never set: tooltip on, magnet off.
let localOverride: ChartPreferencesDto | null = null;

const listeners = new Set<() => void>();

function subscribe(listener: () => void): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}

function emit(): void {
  for (const listener of listeners) listener();
}

export function useChartPreferences(): {
  tooltipEnabled: boolean;
  magnetEnabled: boolean;
  toggle: (patch: { ohlcTooltip?: boolean; magnet?: boolean }) => void;
} {
  const { profile, setChartPreferences } = useAuth();
  const saved = profile?.chartPreferences ?? null;
  const prefs = useSyncExternalStore(subscribe, () => localOverride ?? saved);

  useEffect(() => {
    localOverride = null;
    emit();
  }, [saved]);

  const toggle = (patch: { ohlcTooltip?: boolean; magnet?: boolean }): void => {
    // Persist the whole effective document (not just the toggled flag): a rapid second
    // toggle rewrites both fields, so two in-flight PATCHes converge on the last state
    const effective = localOverride ?? saved;
    const next = {
      ohlcTooltip: patch.ohlcTooltip ?? effective?.ohlcTooltip ?? true,
      magnet: patch.magnet ?? effective?.magnet ?? false,
    };

    localOverride = next;
    emit();
    void setChartPreferences(next);
  };

  return {
    tooltipEnabled: prefs?.ohlcTooltip ?? true,
    magnetEnabled: prefs?.magnet ?? false,
    toggle,
  };
}
