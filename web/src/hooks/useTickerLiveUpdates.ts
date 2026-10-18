import { useEffect, useMemo, useRef } from 'react';
import { HubConnectionBuilder, type HubConnection, LogLevel } from '@microsoft/signalr';

/** An instrument whose open chart asks for live nudges. */
export interface LiveTicker {
  ticker: string;
  exchange: string | null;
}

/**
 * Live chart updates while a page is open: joins the hub and subscribes the given
 * instruments to their per-ticker groups; every `chartUpdate` signal (the candle
 * pipeline fired for that instrument) is handed to `onTick`, which is expected to
 * refetch the chart data through the regular endpoints.
 *
 * The membership itself is the "chart is open" gate: unmount (or navigating to
 * another instrument) stops the connection, the backend group is empty and sends
 * to it are no-ops, so a closed chart receives and costs nothing.
 *
 * Live updates are strictly an enhancement: a failed connect (offline, API down,
 * proxy without WebSocket support) is swallowed - manual refresh keeps working.
 */
export function useTickerLiveUpdates(
  tickers: LiveTicker[],
  onTick: (tick: LiveTicker) => void,
  enabled = true,
): void {
  // Latest callback without tearing down the connection: a page-callback refresh
  // must not reconnect the hub
  const onTickRef = useRef(onTick);
  onTickRef.current = onTick;

  // The connection is rebuilt only when the subscribed instrument set really changes
  const keySig = useMemo(
    () => [...new Set(tickers.map((k) => `${k.ticker}|${k.exchange ?? ''}`))].sort().join(','),
    [tickers],
  );

  useEffect(() => {
    if (!enabled || keySig.length === 0) return;

    const keys = keySig.split(',').map((entry) => {
      const idx = entry.indexOf('|');
      const exchange = idx >= 0 ? entry.slice(idx + 1) : '';
      return { ticker: entry.slice(0, idx), exchange: exchange === '' ? null : exchange };
    });

    let disposed = false;

    // Authorization rides the same-origin session cookie through the WebSocket
    // handshake - nothing to attach here
    const conn: HubConnection = new HubConnectionBuilder()
      .withUrl('/hubs/chart')
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.Error)
      .build();

    const subscribeAll = () => {
      // A rejected invoke means the hub contract drifted (unknown method, auth) and the
      // backend logs nothing of it: without this warning the chart just silently stays static
      for (const k of keys)
        conn.invoke('subscribe', k.ticker, k.exchange).catch((e: unknown) => {
          console.warn(`[hub] subscribe ${k.ticker} (${k.exchange ?? '-'}) failed`, e);
        });
    };

    conn.on('chartUpdate', (payload: { ticker?: string; exchange?: string | null }) => {
      if (typeof payload?.ticker !== 'string' || payload.ticker.length === 0) return;
      onTickRef.current({ ticker: payload.ticker, exchange: payload.exchange ?? null });
    });
    conn.onreconnected(() => subscribeAll());
    // onclose after the exhausted retries leaves the connection dead on purpose:
    // the page stays fully usable through its manual refresh

    conn
      .start()
      .then(() => {
        if (!disposed) subscribeAll();
      })
      .catch((e: unknown) => {
        // Offline or API down is a normal PWA state and live updates stay an enhancement -
        // but a silently dead connection is also the only way a chart ends up static with
        // no explanation, so the console must be able to tell the two stories apart
        console.warn('[hub] live updates unavailable; charts refresh only on demand', e);
      });

    return () => {
      disposed = true;
      conn.stop().catch(() => {});
    };
  }, [enabled, keySig]);
}
